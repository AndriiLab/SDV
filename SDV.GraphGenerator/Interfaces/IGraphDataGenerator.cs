using SDV.DependenciesAnalyzer.Models;
using SDV.GraphGenerator.Services.Models;

namespace SDV.GraphGenerator.Interfaces;

public interface IGraphDataGenerator
{
    void GenerateGraphDataFromTree(Tree tree,
        IDictionary<string, GraphProject> packages,
        bool singleSolutionMode);

    void SetLabels(IReadOnlyDictionary<string, string[]> requestLabels);
}