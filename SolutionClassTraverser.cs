using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Build.Construction;


namespace SymanticCopy;

public class SolutionClassTraverser
{
    private readonly DotNetProjectParser _parser = new DotNetProjectParser();

    public Dictionary<string, string> ParseSolution(string solutionPath)
    {
        var solutionClasses = new Dictionary<string, string>();

        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");
        }

        var solution = SolutionFile.Parse(solutionPath);

        foreach (var project in solution.ProjectsInOrder)
        {
            if (project.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
            {
                var projectClasses = ParseProject(project.AbsolutePath);
                MergeDictionaries(solutionClasses, projectClasses);
            }
        }

        return solutionClasses;
    }

    public Dictionary<string, string> ParseProject(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"Project file not found: {projectPath}");
        }

        var projectDirectory = Path.GetDirectoryName(projectPath);
        var csFiles = GetProjectCsFiles(projectPath, projectDirectory);

        var projectClasses = new Dictionary<string, string>();

        foreach (var filePath in csFiles)
        {
            try
            {
                var fileClasses = _parser.ParseFile(filePath);
                MergeDictionaries(projectClasses, fileClasses);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
            }
        }

        return projectClasses;
    }

    private List<string> GetProjectCsFiles(string projectPath, string projectDirectory)
    {
        // Simple approach - get all .cs files in project directory
        // For more accuracy, we should parse the .csproj file and honor includes/excludes
        return Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories).ToList();
    }

    private void MergeDictionaries(Dictionary<string, string> target, Dictionary<string, string> source)
    {
        foreach (var kvp in source)
        {
            if (target.ContainsKey(kvp.Key))
            {
                // Handle duplicate class names by adding a disambiguator
                var newKey = $"{kvp.Key}_{Path.GetRandomFileName().Substring(0, 4)}";
                target[newKey] = kvp.Value;
            }
            else
            {
                target[kvp.Key] = kvp.Value;
            }
        }
    }
}