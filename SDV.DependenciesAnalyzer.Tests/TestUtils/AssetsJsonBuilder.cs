using System.Text.Json;
using SDV.DependenciesAnalyzer.PackagesConfig;

namespace SDV.DependenciesAnalyzer.Tests.TestUtils;

/// <summary>
/// Fluent builder for <see cref="ProjectAssetsJsonModel"/>, mirroring what NuGet restore writes to obj/project.assets.json.
/// </summary>
public class AssetsJsonBuilder(string csprojPath)
{
    private const string DefaultFramework = "net10.0";

    private readonly Dictionary<string, Dictionary<string, ProjectAssetsTarget>> _targets = new();
    private readonly Dictionary<string, AssetsLibrary> _libraries = new();
    private readonly Dictionary<string, ProjectFramework> _frameworks = new();
    private readonly List<(string Id, string Version)> _packages = [];

    public string ProjectDirectory => Path.GetDirectoryName(csprojPath)!;

    public AssetsJsonBuilder WithDirectDependency(string id, string version, string framework = DefaultFramework)
    {
        var projectFramework = GetOrAdd(_frameworks, framework);
        projectFramework.Dependencies ??= new Dictionary<string, FrameworkDependency>();
        projectFramework.Dependencies[id] = new FrameworkDependency { Target = "Package", Version = $"[{version}, )" };
        return this;
    }

    public AssetsJsonBuilder WithPackage(string id, string version, params string[] dependencies)
    {
        var lowerId = id.ToLowerInvariant();
        _libraries[$"{id}/{version}"] = new AssetsLibrary
        {
            Type = "package",
            Path = $"{lowerId}/{version}",
            Files = [$"{lowerId}.{version}.nupkg.sha512", $"{lowerId}.nuspec"]
        };
        AddTarget(id, version, "package", DefaultFramework, dependencies);
        _packages.Add((id, version));
        return this;
    }

    public AssetsJsonBuilder WithProjectReference(string name, string framework = DefaultFramework)
    {
        _libraries[$"{name}/1.0.0"] = new AssetsLibrary { Type = "project", Path = $"../{name}/{name}.csproj" };
        AddTarget(name, "1.0.0", "project", framework, []);
        return this;
    }

    public ProjectAssetsJsonModel Build() => new()
    {
        Version = 3,
        Targets = _targets,
        Libraries = _libraries,
        Project = new AssetsProject
        {
            Version = "1.0.0",
            Restore = new Restore { ProjectPath = csprojPath, ProjectName = Path.GetFileNameWithoutExtension(csprojPath) },
            Frameworks = _frameworks
        }
    };

    /// <summary>
    /// Writes obj/project.assets.json next to the csproj plus a nupkg for every package where
    /// <c>CommonUtils.FindNupkgFilePath</c> looks first (a <c>packages</c> folder in the project directory).
    /// </summary>
    /// <returns>The path of the written project.assets.json.</returns>
    public string WriteTo()
    {
        WriteNupkgs();
        var path = Path.Combine(ProjectDirectory, "obj", "project.assets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(Build()));
        return path;
    }

    public void WriteNupkgs()
    {
        foreach (var (id, version) in _packages)
        {
            TestFiles.WriteNupkg(NupkgPath(ProjectDirectory, id, version), id, version);
        }
    }

    public static string NupkgPath(string projectDirectory, string id, string version)
    {
        var lowerId = id.ToLowerInvariant();
        return Path.Combine(projectDirectory, "packages", $"{lowerId}.{version}", $"{lowerId}.{version}.nupkg");
    }

    private void AddTarget(string id, string version, string type, string framework, string[] dependencies)
    {
        var target = GetOrAdd(_targets, framework);
        target[$"{id}/{version}"] = new ProjectAssetsTarget
        {
            Type = type,
            Dependencies = dependencies.ToDictionary(d => d, object (_) => "1.0.0")
        };
    }

    private static T GetOrAdd<T>(Dictionary<string, T> dictionary, string key) where T : new()
    {
        if (!dictionary.TryGetValue(key, out var value))
        {
            value = new T();
            dictionary[key] = value;
        }

        return value;
    }
}
