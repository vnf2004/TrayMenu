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

        var order = OrderStore.Load();
        var root = Path.GetFullPath(folderPath);
        var built = BuildDirectoryItems(root, root, order);
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

    private static List<ToolStripItem> BuildDirectoryItems(
        string rootFolder,
        string folderPath,
        IReadOnlyDictionary<string, List<string>> order)
    {
        var result = new List<ToolStripItem>();
        List<string> names;

        try
        {
            names = OrderStore.GetOrderedChildNames(rootFolder, folderPath, order);
        }
        catch
        {
            result.Add(new ToolStripMenuItem("Не удалось прочитать папку") { Enabled = false });
            return result;
        }

        foreach (var name in names)
        {
            var fullPath = Path.Combine(folderPath, name);
            if (Directory.Exists(fullPath))
            {
                var children = BuildDirectoryItems(rootFolder, fullPath, order);
                if (children.Count == 0 || children.All(i => !i.Enabled))
                {
                    continue;
                }

                var subMenu = new ToolStripMenuItem(name);
                foreach (var child in children)
                {
                    subMenu.DropDownItems.Add(child);
                }

                result.Add(subMenu);
            }
            else if (File.Exists(fullPath)
                     && fullPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var item = new ToolStripMenuItem(Path.GetFileNameWithoutExtension(name))
                {
                    Tag = fullPath,
                    Image = TryGetIcon(fullPath)
                };
                var path = fullPath;
                item.Click += (_, _) => LaunchShortcut(path);
                result.Add(item);
            }
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
