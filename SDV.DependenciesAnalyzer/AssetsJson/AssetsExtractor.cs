using System.Text.Json;
using Microsoft.Extensions.Logging;
using SDV.DependenciesAnalyzer.Exceptions;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.DependenciesAnalyzer.PackagesConfig;
using SDV.DependenciesAnalyzer.Structure;
using SDV.DependenciesAnalyzer.Common;

namespace SDV.DependenciesAnalyzer.AssetsJson;

public class AssetsExtractorBuilder : IExtractorBuilder
{
    private readonly ILogger _log;

    public AssetsExtractorBuilder(ILogger log)
    {
        _log = log;
    }
    
    public bool IsCompatible(string projectName, string dependenciesSource)
    {
        if (dependenciesSource.EndsWith("project.assets.json"))
        {
            _log.LogInformation("Found {File} file for project: {Project}", dependenciesSource, projectName);
            return true;
        }

        return false;
    }

    public IExtractor GetExtractor(string dependenciesSource, string csprojPath, string solutionPath)
    {
        return AssetsExtractor.NewExtractor(dependenciesSource, _log);
    }

    private class AssetsExtractor : IExtractor
    {
        private readonly ILogger _log;
        private readonly ProjectAssetsJsonModel _assets;
        private readonly AssetsUtils _utils;
        
        private AssetsExtractor(ILogger log, ProjectAssetsJsonModel assets)
        {
            _log = log;
            _assets = assets;
            _utils = new AssetsUtils(log);
        }
        
        public static IExtractor NewExtractor(string dependenciesSource, ILogger logger)
        {
            var content = CommonUtils.ReadFileIfExists(dependenciesSource);
            if (content == null)
            {
                throw new NugetDepsTreeException("Unable to read file: " + dependenciesSource);
            }

            var assets = JsonSerializer.Deserialize<ProjectAssetsJsonModel>(content);
            if (assets == null)
            {
                throw new NugetDepsTreeException("Project Assets Json not deserialized: " + dependenciesSource);
            }
            return new AssetsExtractor(logger, assets);
        }

        public List<Dependency> DependentProjects()
        {
            return  _utils.GetDependentProjects(_assets);
        }

        public CaseInsensitiveMap<IDependencyDetails> AllDependencies()
        {
            return _utils.GetAllDependencies(_assets);
        }

        public List<string> DirectDependencies()
        {
            return _utils.GetDirectDependencies(_assets);
        }

        public CaseInsensitiveMap<List<string>> ChildrenMap()
        {
            return _utils.GetChildrenMap(_assets);
        }
    }
}
