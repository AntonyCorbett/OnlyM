using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using Serilog;

namespace OnlyM.MediaElementAdaption;

/// <summary>
/// Determines, once per session, which hardware video decoding device type (D3D11VA or DXVA2)
/// can be created on this PC.
/// </summary>
/// <remarks>
/// FFME accepts a single hardware device and falls back to software decoding (not to another
/// device type) if that device can't be created. D3D11VA needs a WDDM 1.2+ display driver, so on
/// older GPUs it fails while DXVA2 still works. Checking up front lets OnlyM choose DXVA2 there,
/// and skip hardware decoding entirely when neither device type is available. The check only
/// covers device creation; whether the GPU supports a particular codec is still handled by
/// FFmpeg's own software fallback.
/// </remarks>
internal static class HardwareDecoderProbe
{
    private static readonly AVHWDeviceType[] PreferredDeviceTypes =
    [
        AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA,
        AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2,
    ];

    private static readonly Lazy<AVHWDeviceType> AvailableDeviceTypeLazy = new(Probe);

    /// <summary>
    /// Gets the preferred hardware device type that can be created on this PC, or
    /// <see cref="AVHWDeviceType.AV_HWDEVICE_TYPE_NONE"/> if none can. Checked on first use.
    /// </summary>
    public static AVHWDeviceType AvailableDeviceType => AvailableDeviceTypeLazy.Value;

    /// <summary>
    /// Runs the check on a background thread after <paramref name="delay"/> so that the first
    /// video doesn't wait for it (it takes ~0.2 s). If a video is opened before the check
    /// completes, opening waits for the result.
    /// </summary>
    public static void CheckInBackground(TimeSpan delay) =>
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay).ConfigureAwait(false);
            _ = AvailableDeviceType; // Probe() catches and logs its own errors
        });

    /// <summary>
    /// Returns the first candidate for which a device can be created, or
    /// <see cref="AVHWDeviceType.AV_HWDEVICE_TYPE_NONE"/>.
    /// </summary>
    internal static AVHWDeviceType SelectDeviceType(
        IReadOnlyList<AVHWDeviceType> candidates,
        Func<AVHWDeviceType, bool> canCreateDevice)
    {
        foreach (var deviceType in candidates)
        {
            if (canCreateDevice(deviceType))
            {
                return deviceType;
            }
        }

        return AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
    }

    internal static string GetDisplayName(AVHWDeviceType deviceType) =>
        deviceType switch
        {
            AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA => "d3d11va",
            AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2 => "dxva2",
            AVHWDeviceType.AV_HWDEVICE_TYPE_NONE => "none",
            _ => deviceType.ToString(),
        };

    private static AVHWDeviceType Probe()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;

        try
        {
            // Normally already loaded by FFME when media is opened. In FFME 4.x the return value
            // is false when the libraries were *already* loaded (not a failure), and it throws if
            // they can't be loaded, so the result is deliberately ignored.
            _ = Unosquare.FFME.Library.LoadFFmpeg();

            result = SelectDeviceType(PreferredDeviceTypes, CanCreateDevice);
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Checking hardware video decoding support");
        }

        Log.Logger.Information(
            "Hardware video decoding available: {DeviceType} (checked in {ElapsedMs} ms)",
            GetDisplayName(result),
            stopwatch.ElapsedMilliseconds);

        return result;
    }

    private static unsafe bool CanCreateDevice(AVHWDeviceType deviceType)
    {
        try
        {
            AVBufferRef* deviceContext = null;
            var resultCode = ffmpeg.av_hwdevice_ctx_create(&deviceContext, deviceType, null, null, 0);

            if (deviceContext != null)
            {
                ffmpeg.av_buffer_unref(&deviceContext);
            }

            if (resultCode < 0)
            {
                Log.Logger.Information(
                    "Could not create {DeviceType} hardware device (error {ErrorCode})",
                    GetDisplayName(deviceType),
                    resultCode);

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Creating {DeviceType} hardware device", GetDisplayName(deviceType));
            return false;
        }
    }
}
