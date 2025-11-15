using Microsoft.Extensions.Logging;
using SDV.DependenciesAnalyzer.AssetsJson;
using SDV.DependenciesAnalyzer.Common;
using SDV.DependenciesAnalyzer.Dependencies;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.DependenciesAnalyzer.Models;
using SDV.DependenciesAnalyzer.PackagesConfig;

namespace SDV.DependenciesAnalyzer.Structure;

public class ProjectBuilder(ILogger log)
{
    private readonly IExtractorBuilder[] _extractorBuilders =
    [
        new AssetsExtractorBuilder(log),
        new PackagesExtractorBuilder(log)
    ];
    private readonly DependenciesUtils _dependenciesUtils = new(log);

    public Project Load(string name, string csprojPath, string dependenciesSource, string solutionPath)
    {
        var extractor = GetCompatibleExtractor(name, dependenciesSource, csprojPath, solutionPath);
        return new Project(name, csprojPath, dependenciesSource, extractor);
    }

    public DependencyTree[] CreateDependencyTree(Project project, TreeGeneratorConfiguration configuration)
    {
        return project.Extractor is not null
            ? _dependenciesUtils.CreateDependencyTree(project.Extractor, configuration)
            : [];
    }

    private IExtractor? GetCompatibleExtractor(string projectName, string dependenciesSource, string csprojPath, string solutionPath)
    {
        var builder = _extractorBuilders.FirstOrDefault(e => e.IsCompatible(projectName, dependenciesSource));
        if (builder is not null) 
            return builder.GetExtractor(dependenciesSource, csprojPath, solutionPath);

        log.LogWarning("Unsupported project dependencies for project: {ProjectName}", projectName);
        return null;

    }

    public class Project
    {
        public string Name { get; }
        public string CsprojPath { get; }
        public string RootPath { get; }
        public string DependenciesSource { get; }
        public IExtractor? Extractor { get; }

        protected internal Project(string name, string csprojPath, string dependenciesSource, IExtractor? extractor)
        {
            Name = name;
            CsprojPath = csprojPath;
            RootPath = CommonUtils.GetProjectDirectoryName(csprojPath);
            DependenciesSource = dependenciesSource;
            Extractor = extractor;
        }
    }
}