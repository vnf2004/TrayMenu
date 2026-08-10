namespace TrayMenu.Tests;

public class OrderStoreTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData(".", "")]
    [InlineData("  ", "")]
    [InlineData(@"Tools/Dev", @"Tools\Dev")]
    [InlineData(@"\Tools\Dev\", @"Tools\Dev")]
    public void NormalizeKey_NormalizesRelativePaths(string input, string expected)
    {
        Assert.Equal(expected, OrderStore.NormalizeKey(input));
    }

    [Fact]
    public void ToRelativeDir_ReturnsEmptyForRoot()
    {
        using var temp = TempDir.Create();
        Assert.Equal(string.Empty, OrderStore.ToRelativeDir(temp.Path, temp.Path));
    }

    [Fact]
    public void ToRelativeDir_ReturnsRelativeChildPath()
    {
        using var temp = TempDir.Create();
        var child = Path.Combine(temp.Path, "Tools", "Dev");
        Directory.CreateDirectory(child);

        Assert.Equal(@"Tools\Dev", OrderStore.ToRelativeDir(temp.Path, child));
    }

    [Fact]
    public void GetOrderedChildNames_WithoutOrder_FoldersThenFilesAlphabetically()
    {
        using var temp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Beta"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Alpha"));
        File.WriteAllText(Path.Combine(temp.Path, "z.rdp"), "");
        File.WriteAllText(Path.Combine(temp.Path, "a.lnk"), "");

        var names = OrderStore.GetOrderedChildNames(temp.Path, temp.Path, new Dictionary<string, List<string>>());

        Assert.Equal(new[] { "Alpha", "Beta", "a.lnk", "z.rdp" }, names);
    }

    [Fact]
    public void GetOrderedChildNames_SkipsHiddenFiles()
    {
        using var temp = TempDir.Create();
        File.WriteAllText(Path.Combine(temp.Path, "visible.txt"), "");
        var hidden = Path.Combine(temp.Path, "secret.txt");
        File.WriteAllText(hidden, "");
        File.SetAttributes(hidden, FileAttributes.Hidden);

        var names = OrderStore.GetOrderedChildNames(temp.Path, temp.Path, new Dictionary<string, List<string>>());

        Assert.Equal(new[] { "visible.txt" }, names);
    }

    [Fact]
    public void GetOrderedChildNames_UsesPreferredOrder_AndAppendsNewItems()
    {
        using var temp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Folder"));
        File.WriteAllText(Path.Combine(temp.Path, "b.lnk"), "");
        File.WriteAllText(Path.Combine(temp.Path, "a.lnk"), "");
        File.WriteAllText(Path.Combine(temp.Path, "new.rdp"), "");

        var order = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = ["b.lnk", "Folder", "a.lnk"]
        };

        var names = OrderStore.GetOrderedChildNames(temp.Path, temp.Path, order);

        Assert.Equal(new[] { "b.lnk", "Folder", "a.lnk", "new.rdp" }, names);
    }

    [Fact]
    public void SetLevelOrder_StoresUnderRelativeKey()
    {
        using var temp = TempDir.Create();
        var sub = Path.Combine(temp.Path, "Tools");
        Directory.CreateDirectory(sub);
        var order = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        OrderStore.SetLevelOrder(order, temp.Path, sub, ["one.lnk", "two.rdp"]);

        Assert.True(order.ContainsKey("Tools"));
        Assert.Equal(new[] { "one.lnk", "two.rdp" }, order["Tools"]);
    }

    [Fact]
    public void RewritePathPrefix_UpdatesKeysAndParentList()
    {
        var order = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = ["Old", "other.lnk"],
            ["Old"] = ["child.lnk"],
            [@"Old\Nested"] = ["deep.lnk"]
        };

        OrderStore.RewritePathPrefix(order, "Old", "New", updateParentList: true);

        Assert.False(order.ContainsKey("Old"));
        Assert.False(order.ContainsKey(@"Old\Nested"));
        Assert.Equal(new[] { "child.lnk" }, order["New"]);
        Assert.Equal(new[] { "deep.lnk" }, order[@"New\Nested"]);
        Assert.Equal(new[] { "New", "other.lnk" }, order[""]);
    }

    [Fact]
    public void ReplaceChildName_UpdatesEntryInParentList()
    {
        var order = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = ["old.lnk", "keep.rdp"]
        };

        OrderStore.ReplaceChildName(order, "", "old.lnk", "new.lnk");

        Assert.Equal(new[] { "new.lnk", "keep.rdp" }, order[""]);
    }

    [Fact]
    public void RemovePath_RemovesFileFromParentList()
    {
        var order = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = ["a.lnk", "b.rdp"]
        };

        OrderStore.RemovePath(order, "a.lnk", isDirectory: false);

        Assert.Equal(new[] { "b.rdp" }, order[""]);
    }

    [Fact]
    public void RemovePath_RemovesDirectoryKeysAndParentEntry()
    {
        var order = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = ["Tools", "x.lnk"],
            ["Tools"] = ["a.lnk"],
            [@"Tools\Sub"] = ["b.lnk"]
        };

        OrderStore.RemovePath(order, "Tools", isDirectory: true);

        Assert.Equal(new[] { "x.lnk" }, order[""]);
        Assert.False(order.ContainsKey("Tools"));
        Assert.False(order.ContainsKey(@"Tools\Sub"));
    }
}
