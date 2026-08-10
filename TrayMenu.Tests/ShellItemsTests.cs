namespace TrayMenu.Tests;

public class ShellItemsTests
{
    [Theory]
    [InlineData(@"C:\apps\Chrome.lnk", true)]
    [InlineData(@"C:\apps\Chrome.LNK", true)]
    [InlineData(@"C:\apps\server.rdp", false)]
    [InlineData(@"C:\apps\readme.txt", false)]
    public void IsShortcut_DetectsLnkExtension(string path, bool expected)
    {
        Assert.Equal(expected, ShellItems.IsShortcut(path));
    }

    [Theory]
    [InlineData(@"C:\menu\Chrome.lnk", "Chrome")]
    [InlineData(@"C:\menu\server.rdp", "server.rdp")]
    [InlineData(@"C:\menu\notes.txt", "notes.txt")]
    public void GetMenuDisplayName_MatchesExplorerRules(string path, string expected)
    {
        Assert.Equal(expected, ShellItems.GetMenuDisplayName(path));
    }

    [Fact]
    public void ToFileNameFromDisplayLabel_AddsLnkForShortcuts()
    {
        var result = ShellItems.ToFileNameFromDisplayLabel(@"C:\menu\Old.lnk", "New Name");
        Assert.Equal("New Name.lnk", result);
    }

    [Fact]
    public void ToFileNameFromDisplayLabel_KeepsExistingLnkSuffix()
    {
        var result = ShellItems.ToFileNameFromDisplayLabel(@"C:\menu\Old.lnk", "New.lnk");
        Assert.Equal("New.lnk", result);
    }

    [Fact]
    public void ToFileNameFromDisplayLabel_LeavesNonShortcutAsIs()
    {
        var result = ShellItems.ToFileNameFromDisplayLabel(@"C:\menu\server.rdp", "host.rdp");
        Assert.Equal("host.rdp", result);
    }

    [Fact]
    public void IsVisible_ReturnsTrueForNormalFile()
    {
        using var temp = TempDir.Create();
        var path = Path.Combine(temp.Path, "normal.txt");
        File.WriteAllText(path, "x");

        Assert.True(ShellItems.IsVisible(path));
    }

    [Fact]
    public void IsVisible_ReturnsFalseForHiddenFile()
    {
        using var temp = TempDir.Create();
        var path = Path.Combine(temp.Path, "hidden.txt");
        File.WriteAllText(path, "x");
        File.SetAttributes(path, FileAttributes.Hidden);

        Assert.False(ShellItems.IsVisible(path));
    }

    [Fact]
    public void IsVisible_ReturnsFalseForSystemFile()
    {
        using var temp = TempDir.Create();
        var path = Path.Combine(temp.Path, "system.txt");
        File.WriteAllText(path, "x");
        File.SetAttributes(path, FileAttributes.System);

        Assert.False(ShellItems.IsVisible(path));
    }

    [Fact]
    public void IsVisible_ReturnsFalseForMissingPath()
    {
        Assert.False(ShellItems.IsVisible(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }
}
