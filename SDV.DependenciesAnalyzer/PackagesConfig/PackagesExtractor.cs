using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using SDV.DependenciesAnalyzer.Exceptions;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.DependenciesAnalyzer.Structure;
using SDV.DependenciesAnalyzer.Common;

namespace SDV.DependenciesAnalyzer.PackagesConfig;

public class PackagesExtractorBuilder : IExtractorBuilder
{
    private readonly ILogger _log;

    public PackagesExtractorBuilder(ILogger log)
    {
        _log = log;
    }

    public bool IsCompatible(string projectName, string dependenciesSource)
    {
        const string packagesFileName = "packages.config";
        if (dependenciesSource.EndsWith(packagesFileName))
        {
            _log.LogInformation("Found {File} file for project: {Project}", dependenciesSource, projectName);
            return true;
        }

        return false;
    }

    public IExtractor GetExtractor(string dependenciesSource, string csprojPath, string solutionPath)
    {
        return PackagesExtractor.NewExtractor(dependenciesSource, csprojPath, solutionPath, _log);
    }

    private class PackagesExtractor : IExtractor
    {
        private readonly ILogger _log;
        private readonly CaseInsensitiveMap<IDependencyDetails> _allDependencies;
        private readonly CaseInsensitiveMap<List<string>> _childrenMap;
        private readonly string _csprojPath;
        private readonly string _solutionPath;

        private PackagesExtractor(ILogger log, string csprojPath, string solutionPath)
        {
            _log = log;
            _csprojPath = csprojPath;
            _solutionPath = solutionPath;
            _allDependencies = new CaseInsensitiveMap<IDependencyDetails>();
            _childrenMap = new CaseInsensitiveMap<List<string>>();
        }

        protected internal static PackagesExtractor NewExtractor(string dependenciesSource, string csprojPath, string solutionPath, ILogger logger)
        {
            var newPkgExtractor = new PackagesExtractor(logger, csprojPath, solutionPath);
            var packagesConfig = LoadPackagesConfig(dependenciesSource);
            newPkgExtractor.Extract(packagesConfig);
            return newPkgExtractor;
        }

        public List<Dependency> DependentProjects()
        {
            var content = CommonUtils.ReadFileIfExists(_csprojPath);
            if (string.IsNullOrEmpty(content))
            {
                throw new NugetDepsTreeException("Unable to read file: " + _csprojPath);
            }

            var csproj =  CommonUtils.ParseXmlToObject<CsprojProjectModel>(content);

            var projectReferences = csproj.ItemGroup
                .Where(i => i.ProjectReference is not null)
                .SelectMany(i => i.ProjectReference!)
                .Select(p => new Dependency(p.Name, string.Empty))
                .ToList();
            
            return projectReferences;
        }

        public CaseInsensitiveMap<IDependencyDetails> AllDependencies()
        {
            return _allDependencies;
        }

        public List<string> DirectDependencies()
        {
            return GetDirectDependencies(_allDependencies, _childrenMap);
        }

        public CaseInsensitiveMap<List<string>> ChildrenMap()
        {
            return _childrenMap;
        }

        private List<string> GetDirectDependencies(CaseInsensitiveMap<IDependencyDetails> allDependencies,
            CaseInsensitiveMap<List<string>> childrenMap)
        {
            var helper = new CaseInsensitiveMap<Dfs>();
            foreach (var id in allDependencies.Keys)
            {
                helper[id] = new Dfs(false, false, false);
            }

            foreach (var id in allDependencies.Keys)
            {
                if (helper[id].Visited)
                {
                    continue;
                }

                SearchRootDependencies(helper, id, allDependencies, childrenMap,
                    new CaseInsensitiveMap<bool> { { id, true } });
            }

            var rootDependencies = helper
                .Where(kv => !kv.Value.NotRoot || kv.Value.Circular)
                .Select(kv => kv.Key)
                .ToList();

            return rootDependencies;
        }

        private void SearchRootDependencies(
            Dictionary<string, Dfs> dfsHelper,
            string currentId,
            Dictionary<string, IDependencyDetails> allDependencies,
            Dictionary<string, List<string>> childrenMap,
            Dictionary<string, bool> traversePath)
        {
            if (dfsHelper[currentId].Visited)
                return;

            if (childrenMap.TryGetValue(currentId, out var children) && children.Count > 0)
            {
                foreach (var next in children)
                {
                    if (!allDependencies.ContainsKey(next))
                        continue;

                    if (traversePath.ContainsKey(next))
                    {
                        foreach (var circular in traversePath.Keys)
                        {
                            var circularDfs = GetDfs(dfsHelper, circular);
                            circularDfs.Circular = true;
                        }

                        continue;
                    }

                    var noRootDfs = GetDfs(dfsHelper, next);
                    noRootDfs.NotRoot = true;
                    traversePath[next] = true;
                    SearchRootDependencies(dfsHelper, next, allDependencies, childrenMap, traversePath);
                    traversePath.Remove(next);
                }
            }

            dfsHelper[currentId].Visited = true;
        }

        private static Dfs GetDfs(Dictionary<string, Dfs> dfsHelper, string key)
        {
            if (!dfsHelper.ContainsKey(key))
            {
                dfsHelper[key] = new Dfs(false, false, false);
            }

            return dfsHelper[key];
        }

        private void Extract(PackagesConfigModel packagesConfig)
        {
            var packages = packagesConfig.Package;
            if (packages != null)
            {
                foreach (var nuget in packages)
                {
                    var id = nuget.Id;
                    var version = nuget.Version;

                    // First let's check if the original version exists within the file system:
                    var pack = CreateNugetPackage(id, version);
                    if (pack == null)
                    {
                        // If it doesn't exist, let's build the array of alternative versions.
                        var alternativeVersions = CreateAlternativeVersionForms(version);
                        // Now let's loop over the alternative possibilities.
                        foreach (var v in alternativeVersions)
                        {
                            pack = CreateNugetPackage(id, v);
                            if (pack != null)
                                break;
                        }
                    }

                    if (pack != null)
                    {
                        _allDependencies[id] = new Dependency(id, version);
                        _childrenMap[id] = pack.Dependencies.Keys.ToList();
                    }
                    else
                    {
                        _log.LogWarning(
                            "The following NuGet package {NugetId} with version {NugetVersion} was not found in the NuGet cache",
                            nuget.Id, nuget.Version);
                    }
                }
            }
        }

        private NugetPackage? CreateNugetPackage(string packageId, string packageVersion)
        {
            var projectPath = CommonUtils.GetProjectDirectoryName(_solutionPath);
            var nupkgPath = CommonUtils.FindNupkgFilePath(
                projectPath,
                Path.Combine(packageId, packageVersion),
                $"{packageId}.{packageVersion}.nupkg");
           if (nupkgPath is null)
            {
                return null;
            }
            
            var nugetPackage = new NugetPackage(packageId, packageVersion);
            
            using var inputStream = new FileStream(nupkgPath, FileMode.Open);
            using var reader = new PackageArchiveReader(inputStream);
            var nuspec = reader.NuspecReader;
            
            foreach (var dependencyGroup in nuspec.GetDependencyGroups())
            {
                foreach (var dependency in dependencyGroup.Packages)
                {
                    nugetPackage.Dependencies[dependency.Id] = true;
                }
            }
            
            return nugetPackage;
        }
        
        private static string[] CreateAlternativeVersionForms(string originalVersion)
        {
            var versionsSlice = originalVersion.Split('.');

            for (int i = 4; i > versionsSlice.Length; i--)
            {
                Array.Resize(ref versionsSlice, versionsSlice.Length + 1);
                versionsSlice[^1] = "0";
            }

            var alternativeVersions = new List<string>();

            for (var i = 4; i > 0; i--)
            {
                var version = string.Join(".", versionsSlice, 0, i);
                if (version != originalVersion)
                {
                    alternativeVersions.Add(version);
                }

                if (!version.EndsWith(".0"))
                {
                    return alternativeVersions.ToArray();
                }
            }

            return alternativeVersions.ToArray();
        }

        private static PackagesConfigModel LoadPackagesConfig(string dependenciesSource)
        {
            var content = CommonUtils.ReadFileIfExists(dependenciesSource);
            if (string.IsNullOrEmpty(content))
            {
                throw new NugetDepsTreeException("Unable to read file: " + dependenciesSource);
            }

            return CommonUtils.ParseXmlToObject<PackagesConfigModel>(content);
        }
    }
}