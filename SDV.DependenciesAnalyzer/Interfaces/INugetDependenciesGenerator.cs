﻿using SDV.DependenciesAnalyzer.Models;

namespace SDV.DependenciesAnalyzer.Interfaces;

public interface INugetDependenciesGenerator
{
    Tree Generate(string slnFilePath);
    Tree Generate(TreeGeneratorConfiguration configuration);
}

public class TreeGeneratorConfiguration
{
    public string SlnFilePath { get; }
    public string[] PackagePrefixes { get; init; }
    public PackageFilterMode Mode { get; init; }
    public bool IncludeDependentProjects { get; init; }

    public TreeGeneratorConfiguration(string slnFilePath)
    {
        SlnFilePath = slnFilePath;
        PackagePrefixes = Array.Empty<string>();
        Mode = PackageFilterMode.None;
        IncludeDependentProjects = false;
    }
}

public enum PackageFilterMode
{
    None = 0,
    Include = 1,
    Exclude = 2
}