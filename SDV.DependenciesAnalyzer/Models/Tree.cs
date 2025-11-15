namespace SDV.DependenciesAnalyzer.Models;

public class Tree(string solutionName)
{
    public string SolutionName { get; } = solutionName;
    public List<Project> Projects { get; } = [];
}