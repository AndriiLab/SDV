namespace SDV.DependenciesAnalyzer.Tests.TestUtils;

/// <summary>
/// A uniquely named directory under the system temp folder, removed with all its content on dispose.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sdv-tests", Guid.NewGuid().ToString("N"));

    public TempDirectory()
    {
        Directory.CreateDirectory(Path);
    }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, ..parts]);

    /// <summary>
    /// Writes <paramref name="content"/> to a file relative to this directory, creating parent folders.
    /// </summary>
    /// <returns>The full path of the written file.</returns>
    public string WriteFile(string relativePath, string content)
    {
        var fullPath = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // best effort: a locked file must not fail the test run
        }
    }
}
