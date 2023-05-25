using System.Xml.Serialization;
using SDV.DependenciesAnalyzer.Exceptions;

namespace SDV.DependenciesAnalyzer.Common;

public class CommonUtils
{
    public static List<string> ListFilesWithExtension(string path, string extension)
    {
        var files = Directory.GetFiles(path)
            .Where(f => Path.GetExtension(f) == extension)
            .Select(f => Path.Combine(path, f))
            .ToList();

        return files;
    }

    public static string? ReadFileIfExists(string filePath)
    {
        return File.Exists(filePath) ? File.ReadAllText(filePath) : null;
    }

    public static string LookPath(string cmd)
    {
        var pathDirectories = Environment.GetEnvironmentVariable("PATH")?.Split(';').Where(s => !string.IsNullOrEmpty(s)) 
                              ?? Enumerable.Empty<string>();
        var exePath = pathDirectories.Select(d => Path.Combine(d, cmd)).FirstOrDefault(File.Exists);
        return exePath ?? string.Empty;
    }

    public static string FixSeparatorsToMatchOs(string path)
    {
        return IsWindows() 
            ? path.Replace('/', '\\')
            : path.Replace('\\', '/');
    }

    public static T ParseXmlToObject<T>(string xmlContent) where T : class, new()
    {
        using var reader = new StringReader(xmlContent);
        var serializer = new XmlSerializer(typeof(T));
        return serializer.Deserialize(reader) as T ?? new T();
    }
    
    public static string GetProjectDirectoryName(string filePath)
    {
        var projectDirectory = Path.GetDirectoryName(filePath);
        if (projectDirectory is null)
        {
            throw new NugetDepsTreeException($"Project directory name name not found for path {filePath}");
        }

        return projectDirectory;
    }
    
    public static string? FindNupkgFilePath(string projectPath, string libraryPath, string nupkgFileName)
    {
        var path = Path.Join(projectPath, "packages", libraryPath.Replace('/', '.').Replace('\\', '.'), nupkgFileName);
        if (File.Exists(path))
        {
            return path;
        }
        var pkgPath = Path.Combine(FixSeparatorsToMatchOs(libraryPath), nupkgFileName);
        path = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", pkgPath.ToLower());

        return File.Exists(path) ? path : null;
    }
    
    private static bool IsWindows()
    {
        return Environment.OSVersion.Platform == PlatformID.Win32NT;
    }
}