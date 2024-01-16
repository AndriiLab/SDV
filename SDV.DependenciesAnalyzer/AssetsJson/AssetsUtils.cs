using Microsoft.Extensions.Logging;
using SDV.DependenciesAnalyzer.Common;
using SDV.DependenciesAnalyzer.Exceptions;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.DependenciesAnalyzer.PackagesConfig;
using SDV.DependenciesAnalyzer.Structure;

namespace SDV.DependenciesAnalyzer.AssetsJson;

public class AssetsUtils
{
    private readonly ILogger _log;

    private const string AbsentNupkgWarnMsg = 
        "Skipping adding this dependency to the dependency tree. This might be because the package already exists in a different NuGet cache, possibly the SDK's NuGetFallbackFolder cache. Removing the package from this cache may resolve the issue.";

    public AssetsUtils(ILogger log)
    {
        _log = log;
    }

    public List<string> GetDirectDependencies(ProjectAssetsJsonModel assets)
    {
        var frameworks = assets.Project.Frameworks;
        if (frameworks is null)
        {
            return [];
        }

        var directDependencies = frameworks
            .SelectMany(f => f.Value.Dependencies?.Keys ?? Enumerable.Empty<string>())
            .ToList();

        return directDependencies;
    }

    public CaseInsensitiveMap<IDependencyDetails> GetAllDependencies(ProjectAssetsJsonModel assets)
    {
        var path = assets.Project.Restore?.ProjectPath;
        if (string.IsNullOrEmpty(path))
        {
            throw new NugetDepsTreeException("Packages path is missing");
        }

        path = CommonUtils.GetProjectDirectoryName(path);
        
        var dependencies = new CaseInsensitiveMap<IDependencyDetails>();
        var libraries = assets.Libraries;
        foreach (var (name, library) in libraries)
        {
            var type = library.Type ?? string.Empty;
            if (type == "project")
            {
                continue;
            }

            var libraryPath = library.Path;
            if (string.IsNullOrEmpty(libraryPath))
            {
                throw new NugetDepsTreeException($"Library {name} path is missing");
            }
            
            var nupkgFileName = GetNupkgFileName(library, libraryPath);
            var nupkgFilePath = CommonUtils.FindNupkgFilePath(CommonUtils.FixSeparatorsToMatchOs(path), libraryPath, nupkgFileName);

            if (string.IsNullOrEmpty(nupkgFilePath))
            {
                if (IsPackagePartOfTargetDependencies(assets, libraryPath))
                {
                    _log.LogWarning(
                        "The file {File} doesn't exist in the NuGet cache directory but it does exist as a target in the assets files. {WarnMessage}",
                        nupkgFilePath, AbsentNupkgWarnMsg);
                    continue;
                }

                throw new NugetDepsTreeException($"The file {nupkgFilePath} doesn't exist in the NuGet cache directory.");
            }

            var dep = GetDependency(name);
            dependencies[dep.Id] = dep;
        }

        return dependencies;
    }

    public CaseInsensitiveMap<List<string>> GetChildrenMap(ProjectAssetsJsonModel assets)
    {
        var dependenciesRelations = new CaseInsensitiveMap<List<string>>();
        var targets = assets.Targets;
        foreach (var (_, targetValue) in targets)
        {
            foreach (var (dependencyKey, dependencyValue) in targetValue)
            {
                var transitive = new List<string>();
                var dependencyDependencies = dependencyValue.Dependencies;
                if (dependencyDependencies != null)
                {
                    foreach (var (transitiveName, _) in dependencyDependencies)
                    {
                        transitive.Add(transitiveName);
                    }
                }

                var dep = GetDependency(dependencyKey);
                dependenciesRelations[dep.Id] = transitive;
            }
        }

        return dependenciesRelations;
    }

    private static Dependency GetDependency(string dependency)
    {
        var splitDependency = dependency.Split('/');
        if (splitDependency.Length != 2)
        {
            throw new NugetDepsTreeException("Unexpected dependency: " + dependency + ". Could not parse id and version");
        }

        return new Dependency(splitDependency[0], splitDependency[1]);
    }

    private static bool IsPackagePartOfTargetDependencies(ProjectAssetsJsonModel assets, string nugetPackageName)
    {
        var targets = assets.Targets;
        foreach (var (_, targetValue) in targets)
        {
            foreach (var (dependencyKey, _) in targetValue)
            {
                // The package names in the targets section of the assets.json file are case insensitive.
                if (string.Compare(dependencyKey, nugetPackageName, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetNupkgFileName(AssetsLibrary library, string libraryPath)
    {
        var files = library.Files;
        if (files != null)
        {
            foreach (var fileName in files)
            {
                if (fileName.EndsWith("nupkg.sha512"))
                {
                    return Path.GetFileNameWithoutExtension(fileName);
                }
            }
        }

        throw new NugetDepsTreeException("Could not find nupkg file name for: " + libraryPath);
    }

    public List<Dependency> GetDependentProjects(ProjectAssetsJsonModel assets)
    {
        return assets.Libraries
            .Where(kv => kv.Value.Type == "project")
            .Select(kv => GetDependency(kv.Key))
            .ToList();
    }
}