namespace TrayMenu;

public static class ShellItems
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static bool IsVisible(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return (attrs & (FileAttributes.Hidden | FileAttributes.System)) == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsShortcut(string path) =>
        path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Explorer-like label: .lnk without extension; other files with extension.
    /// </summary>
    public static string GetMenuDisplayName(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
        {
            return path;
        }

        return IsShortcut(name) ? Path.GetFileNameWithoutExtension(name) : name;
    }

    /// <summary>
    /// Maps an edited display label to the on-disk file name.
    /// Shortcuts keep/restore the .lnk extension; other files use the label as-is.
    /// </summary>
    public static string ToFileNameFromDisplayLabel(string absolutePath, string displayLabel)
    {
        displayLabel = displayLabel.Trim();
        if (IsShortcut(absolutePath))
        {
            if (displayLabel.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return displayLabel;
            }

            return displayLabel + ".lnk";
        }

        return displayLabel;
    }

    /// <summary>
    /// Small shell icon for a file or folder (as in Explorer).
    /// </summary>
    public static Image? TryGetShellIcon(string path)
    {
        var info = new ShFileInfo();
        var result = SHGetFileInfo(
            path,
            0,
            ref info,
            (uint)System.Runtime.InteropServices.Marshal.SizeOf(info),
            ShgfiIcon | ShgfiSmallIcon);

        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using var temp = Icon.FromHandle(info.hIcon);
            return (Image)temp.ToBitmap();
        }
        catch
        {
            return null;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }
}
