namespace TrayMenu;

public static class ShortcutMenuBuilder
{
    public static void Populate(ToolStripItemCollection items, string folderPath)
    {
        items.Clear();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            items.Add(new ToolStripMenuItem("Папка ярлыков не задана") { Enabled = false });
            return;
        }

        var built = BuildDirectoryItems(folderPath);
        if (built.Count == 0)
        {
            items.Add(new ToolStripMenuItem("Нет ярлыков") { Enabled = false });
            return;
        }

        foreach (var item in built)
        {
            items.Add(item);
        }
    }

    private static List<ToolStripItem> BuildDirectoryItems(string folderPath)
    {
        var result = new List<ToolStripItem>();

        IEnumerable<string> subDirs;
        IEnumerable<string> shortcuts;

        try
        {
            subDirs = Directory.EnumerateDirectories(folderPath)
                .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase);
            shortcuts = Directory.EnumerateFiles(folderPath, "*.lnk")
                .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.CurrentCultureIgnoreCase);
        }
        catch
        {
            result.Add(new ToolStripMenuItem("Не удалось прочитать папку") { Enabled = false });
            return result;
        }

        foreach (var dir in subDirs)
        {
            var children = BuildDirectoryItems(dir);
            if (children.Count == 0 || children.All(i => !i.Enabled))
            {
                continue;
            }

            var subMenu = new ToolStripMenuItem(Path.GetFileName(dir));
            foreach (var child in children)
            {
                subMenu.DropDownItems.Add(child);
            }

            result.Add(subMenu);
        }

        foreach (var shortcut in shortcuts)
        {
            var item = new ToolStripMenuItem(Path.GetFileNameWithoutExtension(shortcut))
            {
                Tag = shortcut,
                Image = TryGetIcon(shortcut)
            };
            var path = shortcut;
            item.Click += (_, _) => LaunchShortcut(path);
            result.Add(item);
        }

        return result;
    }

    private static void LaunchShortcut(string shortcutPath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = shortcutPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось запустить ярлык:\n{shortcutPath}\n\n{ex.Message}",
                "TrayMenu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static Image? TryGetIcon(string path)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            return icon?.ToBitmap();
        }
        catch
        {
            return null;
        }
    }
}
