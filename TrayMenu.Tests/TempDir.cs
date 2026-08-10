namespace TrayMenu.Tests;

internal sealed class TempDir : IDisposable
{
    public string Path { get; }

    private TempDir(string path) => Path = path;

    public static TempDir Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TrayMenuTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TempDir(path);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
