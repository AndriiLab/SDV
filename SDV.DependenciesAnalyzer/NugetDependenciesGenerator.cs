using Microsoft.Extensions.Logging;
using SDV.DependenciesAnalyzer.Exceptions;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.DependenciesAnalyzer.Models;
using SDV.DependenciesAnalyzer.Structure;

namespace SDV.DependenciesAnalyzer;
public class NugetDependenciesGenerator : INugetDependenciesGenerator
{
    private readonly ILogger _log;
    private readonly ProjectBuilder _projectBuilder;
    private readonly SolutionBuilder _solutionBuilder;

    public NugetDependenciesGenerator(ILogger<NugetDependenciesGenerator> log)
    {
        _log = log;
        _projectBuilder = new ProjectBuilder(_log);
        _solutionBuilder = new SolutionBuilder(_log, _projectBuilder);
    }
    
    public Tree Generate(string slnFilePath)
    {
        return Generate(new TreeGeneratorConfiguration(slnFilePath, []));
    }

    public Tree Generate(TreeGeneratorConfiguration configuration)
    {
        ValidateFilePath(configuration.SlnFilePath);

        var solution = _solutionBuilder.CreateSolutionFile(configuration);
        
        _log.LogInformation("Found {Number} projects in solution. Processing dependencies", solution.Projects.Count);
       
        return BuildDependenciesTree(solution, configuration);
    }

    private Tree BuildDependenciesTree(SolutionBuilder.Solution solution, TreeGeneratorConfiguration configuration)
    {
        var nugetDepsTree = new Tree(Path.GetFileNameWithoutExtension(configuration.SlnFilePath));
        // Build the tree for each project.
        foreach (var project in solution.Projects)
        {
            var depsTree = _projectBuilder.CreateDependencyTree(project, configuration);
            nugetDepsTree.Projects.Add(new Project(project.Name, depsTree));
        }

        return nugetDepsTree;
    }

    private static void ValidateFilePath(string slnFilePath)
    {
        var attr = File.GetAttributes(slnFilePath);
        if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            throw new NugetDepsTreeException("Provided path is a directory. Please include path to .sln file");
        
        if(!slnFilePath.EndsWith(".sln"))
            throw new NugetDepsTreeException("Provided file is not a solution file. Please include path to .sln file");
    }
}