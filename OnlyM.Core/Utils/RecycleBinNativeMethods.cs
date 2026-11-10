using System;
using System.Runtime.InteropServices;

namespace OnlyM.Core.Utils;
// ReSharper disable InconsistentNaming

/// <summary>
/// Native methods used to send files to the Recycle Bin without any shell UI.
/// </summary>
internal static class RecycleBinNativeMethods
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    /// <summary>
    /// Sends the file to the Recycle Bin, suppressing all shell dialogs (progress, confirmation and error).
    /// If the drive has no Recycle Bin, the shell deletes the file permanently.
    /// </summary>
    /// <param name="fullPath">Fully-qualified path of the file.</param>
    /// <returns>Zero on success, otherwise the shell error code. -1 if the operation was aborted.</returns>
    public static int RecycleSilently(string fullPath)
    {
        // pFrom must be double-null terminated.
        var from = fullPath + '\0' + '\0';
        const ushort flags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT;

        // SHFILEOPSTRUCT is 1-byte packed on 32-bit Windows but naturally aligned on 64-bit,
        // so use the matching struct for the current process.
        if (Environment.Is64BitProcess)
        {
            var op64 = new SHFILEOPSTRUCT64 { wFunc = FO_DELETE, pFrom = from, fFlags = flags };
            var result64 = SHFileOperation64(ref op64);
            return result64 == 0 && op64.fAnyOperationsAborted ? -1 : result64;
        }

        var op32 = new SHFILEOPSTRUCT32 { wFunc = FO_DELETE, pFrom = from, fFlags = flags };
        var result32 = SHFileOperation32(ref op32);
        return result32 == 0 && op32.fAnyOperationsAborted ? -1 : result32;
    }

#pragma warning disable SYSLIB1054 // not possible to use LibraryImport since SHFILEOPSTRUCT contains string fields
    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation32(ref SHFILEOPSTRUCT32 lpFileOp);

    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation64(ref SHFILEOPSTRUCT64 lpFileOp);
#pragma warning restore SYSLIB1054

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    private struct SHFILEOPSTRUCT32
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT64
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }
}
