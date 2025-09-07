using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Build.Construction;

namespace SymanticCopy;

//// Example usage:
//var analyzer = new SolutionPairAnalyzer();

//// Compare two solutions
//var result = analyzer.CompareSolutions(
//    @"C:\path\to\main\solution.sln",
//    @"C:\path\to\franchise\solution.sln");

//// Print the comparison results
//analyzer.PrintComparisonResult(result);

//// Access specific results:
//foreach (var className in result.CommonClassNames)
//{
//    // These are potential candidates for comparison/merging
//    Console.WriteLine($"Common class: {className}");
    
//    if (result.Solution1Duplicates.ContainsKey(className))
//    {
//        Console.WriteLine($"  Warning: Duplicate in Solution 1");
//    }
    
//    if (result.Solution2Duplicates.ContainsKey(className))
//{
//    Console.WriteLine($"  Warning: Duplicate in Solution 2");
//}
//}

public class SolutionPairAnalyzer
{
    public class AnalysisResult
    {
        public List<string> CommonClassNames { get; } = new List<string>();

        public Dictionary<string, List<string>> Solution1Duplicates { get; } = new Dictionary<string, List<string>>();

        public Dictionary<string, List<string>> Solution2Duplicates { get; } = new Dictionary<string, List<string>>();

        public List<string> Solution1Unique { get; } = new List<string>();

        public List<string> Solution2Unique { get; } = new List<string>();
    }

    public AnalysisResult CompareSolutions(string solution1Path, string solution2Path)
    {
        var result = new AnalysisResult();

        // Process both solutions in memory-efficient way
        var solution1Classes = GetSolutionClassNames(solution1Path);
        var solution2Classes = GetSolutionClassNames(solution2Path);

        // Find common classes
        var allClassNames = new HashSet<string>(
				solution1Classes
				.Keys
				.Concat(solution2Classes.Keys));

        foreach (var className in allClassNames)
        {
            bool inSolution1 = solution1Classes.ContainsKey(className);
            bool inSolution2 = solution2Classes.ContainsKey(className);

            if (inSolution1 && inSolution2)
            {
                result.CommonClassNames.Add(className);

                // Check for duplicates in each solution
                if (solution1Classes[className].Count > 1)
                {
                    result.Solution1Duplicates[className] = solution1Classes[className];
                }

                if (solution2Classes[className].Count > 1)
                {
                    result.Solution2Duplicates[className] = solution2Classes[className];
                }
            }
            else if (inSolution1)
            {
                result.Solution1Unique.Add(className);

                if (solution1Classes[className].Count > 1)
                {
                    result.Solution1Duplicates[className] = solution1Classes[className];
                }
            }
            else
            {
                result.Solution2Unique.Add(className);

                if (solution2Classes[className].Count > 1)
                {
                    result.Solution2Duplicates[className] = solution2Classes[className];
                }
            }
        }

        return result;
    }

    private Dictionary<string, List<string>> GetSolutionClassNames(string solutionPath)
    {
        var classMap = new Dictionary<string, List<string>>();

        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException(
			$"Solution file not found: {solutionPath}");
        }

        var solution = SolutionFile.Parse(solutionPath);

        foreach (var project in solution.ProjectsInOrder)
        {
            if (project.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
            {
                var projectDirectory = Path.GetDirectoryName(project.AbsolutePath);
                
		var csFiles = Directory.GetFiles(
					projectDirectory, 
					"*.cs", 
					SearchOption.AllDirectories);

                foreach (var filePath in csFiles)
                {
                    try
                    {
                        var fileContent = File.ReadAllText(filePath);
                        
			var syntaxTree = CSharpSyntaxTree.ParseText(fileContent);
                        var root = syntaxTree.GetRoot();

                        // Get all type declarations
                        var typeDeclarations = root.DescendantNodes()
                            .Where(n => n is ClassDeclarationSyntax
                                     || n is StructDeclarationSyntax
                                     || n is RecordDeclarationSyntax
                                     || n is InterfaceDeclarationSyntax);

                        foreach (var typeDecl in typeDeclarations)
                        {
                            var typeName = typeDecl switch
                            {
                                ClassDeclarationSyntax classDecl => classDecl.Identifier.Text,
                        
			        StructDeclarationSyntax structDecl => structDecl.Identifier.Text,
                        
			        RecordDeclarationSyntax recordDecl => recordDecl.Identifier.Text,
                        
			        InterfaceDeclarationSyntax interfaceDecl => interfaceDecl.Identifier.Text,
                        
			        _ => throw new NotSupportedException()
                            };

                            if (!classMap.ContainsKey(typeName))
                            {
                                classMap[typeName] = new List<string>();
                            }

                            classMap[typeName].Add(GetRelativePath(filePath, solutionPath));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
				$"Error processing file {filePath}: {ex.Message}");
                    }
                }
            }
        }

        return classMap;
    }

    private string GetRelativePath(string fullPath, string solutionPath)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath);
        
	return Path.GetRelativePath(solutionDir, fullPath);
    }

    public void PrintComparisonResult(AnalysisResult result)
    {
        Console.WriteLine("=== Solution Comparison Results ===");
        Console.WriteLine($"Common classes: {result.CommonClassNames.Count}");
        Console.WriteLine($"Solution 1 unique classes: {result.Solution1Unique.Count}");
        Console.WriteLine($"Solution 2 unique classes: {result.Solution2Unique.Count}");

        if (result.Solution1Duplicates.Count > 0)
        {
            Console.WriteLine("\nDuplicate classes in Solution 1:");
        
	    foreach (var kvp in result.Solution1Duplicates)
            {
                Console.WriteLine($"- {kvp.Key} ({kvp.Value.Count} occurrences)");
        
	        foreach (var path in kvp.Value)
                {
                    Console.WriteLine($"  - {path}");
                }
            }
        }

        if (result.Solution2Duplicates.Count > 0)
        {
            Console.WriteLine("\nDuplicate classes in Solution 2:");
        
	    foreach (var kvp in result.Solution2Duplicates)
            {
                Console.WriteLine($"- {kvp.Key} ({kvp.Value.Count} occurrences)");
        
	        foreach (var path in kvp.Value)
                {
                    Console.WriteLine($"  - {path}");
                }
            }
        }
    }
}
