namespace TrayMenu;

public static class ShellItems
{
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
}
