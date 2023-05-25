using SDV.DependenciesAnalyzer.Structure;
using SDV.DependenciesAnalyzer.Common;

namespace SDV.DependenciesAnalyzer.Interfaces;

public interface IExtractor
{
    List<Dependency> DependentProjects();
    CaseInsensitiveMap<IDependencyDetails> AllDependencies();
    List<string> DirectDependencies();
    CaseInsensitiveMap<List<string>> ChildrenMap();
}

public interface IExtractorBuilder
{
    bool IsCompatible(string projectName, string dependenciesSource);
    IExtractor GetExtractor(string dependenciesSource, string csprojPath, string solutionPath);

}