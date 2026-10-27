using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Serilog;

namespace OnlyM.Services.RemoteControl;

/// <summary>
/// Runs "cloudflared tunnel --url http://localhost:port --http-host-header localhost:port" (a Cloudflare Quick Tunnel)
/// as a child process and captures the public trycloudflare.com URL. The Host header is rewritten to localhost so
/// http.sys accepts the requests even when the listener fell back to http://localhost:port/.
/// The process is assigned to a Job Object so it dies with OnlyM, even after a crash.
/// </summary>
internal sealed partial class CloudflareTunnel : IDisposable
{
    private static readonly TimeSpan UrlTimeout = TimeSpan.FromSeconds(30);

    private readonly object _lock = new();

    // Serialises whole Start/Stop calls (process launch and kill), so they can't interleave
    private readonly object _lifecycleLock = new();
    private Process? _process;
    private IntPtr _job;
    private System.Threading.Timer? _timeoutTimer;
    private string? _lastLine;

    public event EventHandler? StateChanged;

    public string? PublicUrl { get; private set; }

    public string? Error { get; private set; }

    public TunnelState State { get; private set; } = TunnelState.Idle;

    /// <summary>
    /// Marks the tunnel as starting (so the UI can show progress immediately);
    /// call <see cref="Start"/> afterwards, typically on a background thread.
    /// </summary>
    /// <returns>False if the tunnel is already running or starting (so no new start is needed).</returns>
    public bool BeginStart()
    {
        lock (_lock)
        {
            if (_process != null || State == TunnelState.Starting)
            {
                return false;
            }

            PublicUrl = null;
            Error = null;
            State = TunnelState.Starting;
        }

        RaiseStateChanged();
        return true;
    }

    /// <summary>
    /// Puts a stopped tunnel into the Failed state with the given message (e.g. the tunnel may not start yet).
    /// </summary>
    public void SetFailed(string message)
    {
        lock (_lock)
        {
            if (_process != null)
            {
                return;
            }

            PublicUrl = null;
            Error = message;
            State = TunnelState.Failed;
        }

        RaiseStateChanged();
    }

    public bool IsRunning => _process is { HasExited: false };

    public static string? ParseUrl(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        var m = UrlRegex().Match(line);
        return m.Success ? m.Value : null;
    }

    /// <summary>
    /// Gets the per-user folder used when cloudflared is installed from Settings (no admin needed).
    /// </summary>
    public static string UserInstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OnlyM", "cloudflared");

    /// <summary>
    /// Returns the full path of cloudflared.exe if it can be found, otherwise null.
    /// </summary>
    public static string? LocateCloudflared()
    {
        var found = FindCloudflared();
        return found.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) ? found : null;
    }

    /// <summary>
    /// Locates cloudflared.exe: the per-user install folder first, then PATH (including the persisted
    /// machine/user PATH, which may be newer than this process's copy), then common install locations.
    /// Falls back to "cloudflared" so the OS does a normal PATH search.
    /// </summary>
    private static string FindCloudflared()
    {
        const string exe = "cloudflared.exe";
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Order: per-user install from Settings, PATH (process + machine + user, as the process copy may be stale),
        // WinGet links, WinGet package folders, Program Files, Program Files (x86).
        var candidates = new List<string> { Path.Combine(UserInstallDir, exe) };

        var paths = string.Join(
            ";",
            Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? string.Empty,
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty);

        foreach (var dir in paths.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                candidates.Add(Path.Combine(Environment.ExpandEnvironmentVariables(dir), exe));
            }
            catch (ArgumentException)
            {
                // ignore malformed PATH entries
            }
        }

        candidates.Add(Path.Combine(localAppData, "Microsoft", "WinGet", "Links", exe));

        try
        {
            var packages = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(packages))
            {
                foreach (var dir in Directory.EnumerateDirectories(packages, "Cloudflare.cloudflared*"))
                {
                    candidates.Add(Path.Combine(dir, exe));
                    candidates.AddRange(Directory.EnumerateFiles(dir, exe, SearchOption.AllDirectories));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Logger.Debug(ex, "Could not search WinGet packages for cloudflared");
        }

        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "cloudflared", exe));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "cloudflared", exe));

        var found = candidates.FirstOrDefault(File.Exists);
        Log.Logger.Debug("cloudflared location: {Path}", found ?? "(not found, relying on PATH)");
        return found ?? "cloudflared";
    }

    public void Start(int port)
    {
        lock (_lifecycleLock)
        {
            StartCore(port);
        }
    }

    public void Stop()
    {
        lock (_lifecycleLock)
        {
            StopCore();
        }
    }

    public void Dispose()
    {
        Stop();
        _timeoutTimer?.Dispose();
        if (_job != IntPtr.Zero)
        {
            CloseHandle(_job);
            _job = IntPtr.Zero;
        }
    }

    private void StartCore(int port)
    {
        lock (_lock)
        {
            if (_process != null || State != TunnelState.Starting)
            {
                return; // already running, or stopped before this (background) start ran
            }

            PublicUrl = null;
            Error = null;
            _lastLine = null;

            var psi = new ProcessStartInfo(FindCloudflared(), $"tunnel --no-autoupdate --url http://localhost:{port} --http-host-header localhost:{port}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // Handlers capture their own process: events from a previous (stopped) process are ignored
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => HandleLine(p, e.Data);
            p.ErrorDataReceived += (_, e) => HandleLine(p, e.Data);
            p.Exited += (_, _) => HandleExited(p);

            try
            {
                p.Start();
            }
            catch (Win32Exception ex)
            {
                p.Dispose();
                Error = Properties.Resources.REMOTE_CONTROL_TUNNEL_CLOUDFLARED_NOT_FOUND;
                State = TunnelState.Failed;
                Log.Logger.Error(ex, "Could not start cloudflared. Is it installed (Settings => Install Cloudflare tunnel) or on PATH?");
                RaiseStateChanged();
                return;
            }

            AssignToJob(p);
            _process = p;
            State = TunnelState.WaitingForUrl;
            _timeoutTimer?.Dispose();
            _timeoutTimer = new System.Threading.Timer(_ => HandleTimeout(p), null, UrlTimeout, System.Threading.Timeout.InfiniteTimeSpan);
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            Log.Logger.Information("Cloudflare tunnel started (cloudflared pid {Pid}) for http://localhost:{Port}", p.Id, port);
        }

        RaiseStateChanged();
    }

    private void StopCore()
    {
        Process? p;
        lock (_lock)
        {
            p = _process;
            _process = null;
            PublicUrl = null;
            _timeoutTimer?.Dispose();
            _timeoutTimer = null;
            var wasIdle = State == TunnelState.Idle;
            State = TunnelState.Idle;
            Error = null;
            if (p == null && !wasIdle)
            {
                // e.g. stopped while Starting or after a failure: let the UI clear progress / errors
                System.Threading.Tasks.Task.Run(RaiseStateChanged);
            }
        }

        if (p != null)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(3000);
                }

                Log.Logger.Information("Cloudflare tunnel stopped");
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                Log.Logger.Warning(ex, "Could not stop cloudflared cleanly");
            }
            finally
            {
                p.Dispose();
            }

            RaiseStateChanged();
        }
    }

    [GeneratedRegex(@"https://[a-z0-9-]+\.trycloudflare\.com", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    private void HandleLine(Process p, string? line)
    {
        if (line == null)
        {
            return;
        }

        Log.Logger.Debug("cloudflared: {Line}", line);
        var url = ParseUrl(line);
        lock (_lock)
        {
            if (!ReferenceEquals(p, _process))
            {
                return; // output from a process that has been stopped or replaced
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                _lastLine = line.Trim();
            }

            if (url == null || PublicUrl != null || url.StartsWith("https://api.", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PublicUrl = url;
            State = TunnelState.Connected;
            _timeoutTimer?.Dispose();
            _timeoutTimer = null;
        }

        Log.Logger.Information("Cloudflare tunnel URL obtained: {Url}", url);
        RaiseStateChanged();
    }

    private void HandleExited(Process p)
    {
        int? code = null;
        try
        {
            code = p.ExitCode;
        }
        catch (InvalidOperationException)
        {
            // ignore
        }

        lock (_lock)
        {
            if (!ReferenceEquals(p, _process))
            {
                return; // stopped deliberately, or a stale exit from a previous process
            }

            _process = null;
            PublicUrl = null;
            _timeoutTimer?.Dispose();
            _timeoutTimer = null;
            State = TunnelState.Failed;
            Error = string.Format(CultureInfo.CurrentCulture, Properties.Resources.REMOTE_CONTROL_TUNNEL_EXITED, code) +
                    (string.IsNullOrEmpty(_lastLine) ? string.Empty : $": {_lastLine}");
        }

        Log.Logger.Error("cloudflared exited unexpectedly with code {Code}; last output: {Line}", code, _lastLine);
        RaiseStateChanged();
    }

    private void HandleTimeout(Process p)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(p, _process) || PublicUrl != null)
            {
                return;
            }

            _process = null;
            _timeoutTimer?.Dispose();
            _timeoutTimer = null;
            State = TunnelState.Failed;
            Error = string.Format(CultureInfo.CurrentCulture, Properties.Resources.REMOTE_CONTROL_TUNNEL_NO_URL, UrlTimeout.TotalSeconds.ToString("0", CultureInfo.CurrentCulture)) +
                    (string.IsNullOrEmpty(_lastLine) ? string.Empty : $": {_lastLine}");
        }

        Log.Logger.Error("cloudflared gave no public URL within {Seconds} s; killing it. Last output: {Line}", UrlTimeout.TotalSeconds, _lastLine);
        try
        {
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            Log.Logger.Warning(ex, "Could not kill cloudflared after timeout");
        }
        finally
        {
            p.Dispose();
        }

        RaiseStateChanged();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void AssignToJob(Process p)
    {
        try
        {
            if (_job == IntPtr.Zero)
            {
                _job = CreateJobObject(IntPtr.Zero, null);
                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION { LimitFlags = JobObjectLimitKillOnJobClose },
                };
                var len = Marshal.SizeOf(info);
                var ptr = Marshal.AllocHGlobal(len);
                try
                {
                    Marshal.StructureToPtr(info, ptr, false);
                    SetInformationJobObject(_job, JobObjectExtendedLimitInformationClass, ptr, (uint)len);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }

            if (!AssignProcessToJobObject(_job, p.Handle))
            {
                Log.Logger.Warning("Could not assign cloudflared to a job object (error {Error})", Marshal.GetLastWin32Error());
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Log.Logger.Warning(ex, "Could not assign cloudflared to a job object");
        }
    }

#pragma warning disable SA1201, SA1202, SA1310, SA1307, SA1600, SA1602
    private const uint JobObjectLimitKillOnJobClose = 0x2000;
    private const int JobObjectExtendedLimitInformationClass = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoClass, IntPtr lpInfo, uint cbInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
#pragma warning restore SA1201, SA1202, SA1310, SA1307, SA1600, SA1602
}