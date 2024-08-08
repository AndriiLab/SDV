using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SDV.DependenciesAnalyzer.Exceptions;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.DependenciesAnalyzer.Common;

namespace SDV.DependenciesAnalyzer.Structure;

public class SolutionBuilder
{
    private readonly ILogger _logger;
    private readonly ProjectBuilder _projectBuilder;
    private const string PackagesFileName = "packages.config";
    private const string AssetsFileName = "project.assets.json";
    private List<string> _dependenciesSources;
    private TreeGeneratorConfiguration _configuration;

    public SolutionBuilder(ILogger logger, ProjectBuilder projectBuilder)
    {
        _logger = logger;
        _projectBuilder = projectBuilder;
        _configuration = new TreeGeneratorConfiguration(string.Empty, [], []);
        _dependenciesSources = new List<string>();
    }

    public Solution CreateSolutionFile(TreeGeneratorConfiguration configuration)
    {
        _configuration = configuration;
        _dependenciesSources = GetDependenciesSources();
        
        var projects = LoadProjects().ToList();
        
        return new Solution(projects);
    }
    
    private IEnumerable<ProjectBuilder.Project> LoadProjects()
    {
        var projectDeclarations = ParseSlnFile(_configuration.SlnFilePath);
        return projectDeclarations.Count > 0 
            ? LoadProjectsFromSolutionFile(projectDeclarations) 
            : LoadSingleProjectFromDir();
    }

    private IEnumerable<ProjectBuilder.Project> LoadProjectsFromSolutionFile(IEnumerable<string> slnProjects)
    {
        return slnProjects.SelectMany(ParseAndLoadProject);
    }

    private IEnumerable<ProjectBuilder.Project> ParseAndLoadProject(string project)
    {
        try
        {
            var parsed = ParseProject(project, CommonUtils.GetProjectDirectoryName(_configuration.SlnFilePath));
            if (!CanProjectBeProcessed(parsed.ProjectName, parsed.CsprojPath))
            {
                return [];
            }
            var pb = LoadProject(parsed.ProjectName, parsed.CsprojPath);
            return pb != null ? new[] { pb } : Enumerable.Empty<ProjectBuilder.Project>(); 
        }
        catch (Exception ex) 
        {
            _logger.LogError(ex, "Failed parsing and loading project '{ProjectName}' from solution '{SolutionName}': {Error}", project, _configuration.SlnFilePath, ex.Message);
            return [];
        }
    }

    private List<string> GetDependenciesSources()
    {
        var curDependenciesSources = new List<string>();
        var dirName = Path.GetDirectoryName(_configuration.SlnFilePath);
        if (dirName is null)
        {
            _logger.LogError("Directory name not found for provided path {Path}", _configuration.SlnFilePath);
            return curDependenciesSources;
        }
        
        foreach (var path in Directory.EnumerateFiles(dirName, "*", SearchOption.AllDirectories))
        {
            if (path.EndsWith(PackagesFileName) || path.EndsWith(AssetsFileName))
            {
                curDependenciesSources.Add(Path.GetFullPath(path));
                _logger.LogInformation("Found: {SlnFile}", path);
            }
        }
        curDependenciesSources.Sort((a, b) => a.Length.CompareTo(b.Length));
        curDependenciesSources.AddRange(curDependenciesSources);
        
        return curDependenciesSources;
    }

    private bool CanProjectBeProcessed(string name, string path)
    {
        if (!path.EndsWith(".csproj"))
        {
            _logger.LogWarning("Skipping a project '{ProjectName}', since it doesn't have a csproj file path", name);
            return false;
        }

        return _configuration.IsPackageEnabled(name);
    }

    private IEnumerable<ProjectBuilder.Project> LoadSingleProjectFromDir()
    {
        var csprojFiles = CommonUtils.ListFilesWithExtension(CommonUtils.GetProjectDirectoryName(_configuration.SlnFilePath), ".csproj");
        if (csprojFiles.Count == 1)
        {
            var projectName = Path.GetFileNameWithoutExtension(csprojFiles[0]);
            if (!CanProjectBeProcessed(projectName, csprojFiles[0]))
            {
                yield break;
            }
            var project = LoadProject(projectName, csprojFiles[0]);
            if (project is not null)
            {
                yield return project;
            }
        }
        _logger.LogWarning("Expected only one undeclared project in solution dir. Found: {Count}", csprojFiles.Count);
    }

    private ProjectBuilder.Project? LoadProject(string projectName, string csprojPath)
    {
        var projectRootPath = CommonUtils.GetProjectDirectoryName(csprojPath);
        var projectPathPattern = $"{projectRootPath}{Path.DirectorySeparatorChar}";
        var projectNamePattern = string.Format("{0}{1}{0}", Path.DirectorySeparatorChar, projectName);
        var dependenciesSource = _dependenciesSources
            .FirstOrDefault(s => s.Contains(projectPathPattern) || s.Contains(projectNamePattern)) ?? string.Empty;

        if (string.IsNullOrEmpty(dependenciesSource))
        {
            _logger.LogWarning("Project dependencies were not found for project: {ProjectName}", projectName);
        }

        var proj = _projectBuilder.Load(projectName, csprojPath, dependenciesSource, _configuration.SlnFilePath);
        if (proj.Extractor != null)
        {
            return proj;
        }

        return null;
    }

    private static ParsedProject ParseProject(string projectLine, string solutionDir)
    {
        var parsedLine = projectLine.Split('=');
        if (parsedLine.Length <= 1)
        {
            throw new NugetDepsTreeException("Unexpected project line format: " + projectLine);
        }

        var projectInfo = parsedLine[1].Split(',');
        if (projectInfo.Length <= 2)
        {
            throw new NugetDepsTreeException("Unexpected project information format: " + parsedLine[1]);
        }

        var projectName = RemoveQuotes(projectInfo[0]);
        projectInfo[1] = CommonUtils.FixSeparatorsToMatchOs(projectInfo[1]);
        var csprojPath = Path.Combine(solutionDir, RemoveQuotes(projectInfo[1]));
        return new ParsedProject(projectName, csprojPath);
    }

    private static string RemoveQuotes(string value)
    {
        return value.Trim().Trim('"');
    }

    private static List<string> ParseSlnFile(string filePath)
    {
        var projects = new List<string>();
        var content = File.ReadAllText(filePath);
        foreach (Match match in Regex.Matches(content, "Project\\(\"(.*)(\\r\\n|\\r|\\n)EndProject"))
        {
            projects.Add(match.Value);
        }
        return projects;
    }

    private record ParsedProject(string ProjectName, string CsprojPath);
        
    public class Solution
    {
        public List<ProjectBuilder.Project> Projects { get; }

        internal Solution(List<ProjectBuilder.Project> projects)
        {
            Projects = projects;
        }
    }
}
