using System.Text.Json;

namespace TrayMenu;

public static class OrderStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string OrderPath => Path.Combine(ConfigStore.ConfigDirectory, "order.json");

    /// <summary>
    /// Keys: relative directory path from shortcuts root ("" = root).
    /// Values: ordered child names (folder names or *.lnk file names).
    /// </summary>
    public static Dictionary<string, List<string>> Load()
    {
        try
        {
            if (!File.Exists(OrderPath))
            {
                return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }

            var json = File.ReadAllText(OrderPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, JsonOptions);
            return data is null
                ? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, List<string>>(data, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void Save(Dictionary<string, List<string>> order)
    {
        Directory.CreateDirectory(ConfigStore.ConfigDirectory);
        var json = JsonSerializer.Serialize(order, JsonOptions);
        File.WriteAllText(OrderPath, json);
    }

    public static string ToRelativeDir(string rootFolder, string absoluteDirectory)
    {
        var root = Path.GetFullPath(rootFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var dir = Path.GetFullPath(absoluteDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(root, dir, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var rel = Path.GetRelativePath(root, dir);
        return NormalizeKey(rel);
    }

    public static string NormalizeKey(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath == ".")
        {
            return string.Empty;
        }

        return relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar);
    }

    public static List<string> GetOrderedChildNames(
        string rootFolder,
        string absoluteDirectory,
        IReadOnlyDictionary<string, List<string>> order)
    {
        List<string> dirs;
        List<string> shortcuts;
        try
        {
            dirs = Directory.EnumerateDirectories(absoluteDirectory)
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Cast<string>()
                .ToList();
            shortcuts = Directory.EnumerateFiles(absoluteDirectory, "*.lnk")
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Cast<string>()
                .ToList();
        }
        catch
        {
            return [];
        }

        var existing = new HashSet<string>(dirs.Concat(shortcuts), StringComparer.OrdinalIgnoreCase);
        var key = ToRelativeDir(rootFolder, absoluteDirectory);

        if (order.TryGetValue(key, out var preferred) && preferred.Count > 0)
        {
            var result = new List<string>();
            foreach (var name in preferred)
            {
                if (existing.Remove(name))
                {
                    result.Add(GetActualName(dirs, shortcuts, name));
                }
            }

            // New items: folders first, then shortcuts, alphabetical
            result.AddRange(dirs.Where(d => existing.Contains(d))
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase));
            result.AddRange(shortcuts.Where(s => existing.Contains(s))
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase));
            return result;
        }

        return dirs.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .Concat(shortcuts.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
            .ToList();
    }

    public static void SetLevelOrder(
        Dictionary<string, List<string>> order,
        string rootFolder,
        string absoluteDirectory,
        IEnumerable<string> childNames)
    {
        var key = ToRelativeDir(rootFolder, absoluteDirectory);
        order[key] = childNames.ToList();
    }

    public static void RewritePathPrefix(
        Dictionary<string, List<string>> order,
        string oldRelativePrefix,
        string newRelativePrefix,
        bool updateParentList = true)
    {
        oldRelativePrefix = NormalizeKey(oldRelativePrefix);
        newRelativePrefix = NormalizeKey(newRelativePrefix);

        if (string.Equals(oldRelativePrefix, newRelativePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var updates = new List<(string OldKey, string NewKey, List<string> Value)>();
        foreach (var pair in order)
        {
            var key = pair.Key;
            if (string.Equals(key, oldRelativePrefix, StringComparison.OrdinalIgnoreCase))
            {
                updates.Add((key, newRelativePrefix, pair.Value));
            }
            else if (IsChildKey(key, oldRelativePrefix))
            {
                var suffix = key[(oldRelativePrefix.Length + 1)..];
                var newKey = string.IsNullOrEmpty(newRelativePrefix)
                    ? suffix
                    : newRelativePrefix + Path.DirectorySeparatorChar + suffix;
                updates.Add((key, NormalizeKey(newKey), pair.Value));
            }
        }

        foreach (var (oldKey, _, _) in updates)
        {
            order.Remove(oldKey);
        }

        foreach (var (_, newKey, value) in updates)
        {
            order[newKey] = value;
        }

        if (!updateParentList)
        {
            return;
        }

        // Rename entry in parent level list (same-parent rename)
        var oldName = string.IsNullOrEmpty(oldRelativePrefix) ? null : Path.GetFileName(oldRelativePrefix);
        var newName = string.IsNullOrEmpty(newRelativePrefix) ? null : Path.GetFileName(newRelativePrefix);
        var parentKey = string.IsNullOrEmpty(oldRelativePrefix)
            ? null
            : NormalizeKey(Path.GetDirectoryName(oldRelativePrefix) ?? string.Empty);

        if (oldName is not null && newName is not null && parentKey is not null)
        {
            if (order.TryGetValue(parentKey, out var siblings))
            {
                for (var i = 0; i < siblings.Count; i++)
                {
                    if (string.Equals(siblings[i], oldName, StringComparison.OrdinalIgnoreCase))
                    {
                        siblings[i] = newName;
                    }
                }
            }
        }
    }

    public static void ReplaceChildName(
        Dictionary<string, List<string>> order,
        string parentRelativeKey,
        string oldName,
        string newName)
    {
        parentRelativeKey = NormalizeKey(parentRelativeKey);
        if (!order.TryGetValue(parentRelativeKey, out var siblings))
        {
            return;
        }

        for (var i = 0; i < siblings.Count; i++)
        {
            if (string.Equals(siblings[i], oldName, StringComparison.OrdinalIgnoreCase))
            {
                siblings[i] = newName;
            }
        }
    }

    public static void RemovePath(
        Dictionary<string, List<string>> order,
        string relativePath,
        bool isDirectory)
    {
        relativePath = NormalizeKey(relativePath);
        var name = Path.GetFileName(relativePath);
        var parentKey = NormalizeKey(Path.GetDirectoryName(relativePath) ?? string.Empty);

        if (order.TryGetValue(parentKey, out var siblings))
        {
            siblings.RemoveAll(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));
        }

        if (isDirectory)
        {
            var toRemove = order.Keys
                .Where(k => string.Equals(k, relativePath, StringComparison.OrdinalIgnoreCase)
                            || IsChildKey(k, relativePath))
                .ToList();
            foreach (var key in toRemove)
            {
                order.Remove(key);
            }
        }
    }

    private static bool IsChildKey(string key, string parentPrefix)
    {
        if (string.IsNullOrEmpty(parentPrefix))
        {
            return !string.IsNullOrEmpty(key);
        }

        return key.StartsWith(parentPrefix + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetActualName(List<string> dirs, List<string> shortcuts, string name)
    {
        var d = dirs.FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
        if (d is not null)
        {
            return d;
        }

        var s = shortcuts.FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
        return s ?? name;
    }
}
