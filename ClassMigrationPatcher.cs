using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Build.Construction;

namespace SymanticCopy;

//var patcher = new ClassMigrationPatcher();
//patcher.MigrateClass(
//    sourceFilePath: @"C:\source\FileA.cs",
//    destinationFilePath: @"C:\destination\FileB.cs",
//    className: "MyClass");

//var patcher = new ClassMigrationPatcher();

//// Define which classes to migrate from where
//var classMigrationMap = new Dictionary<string, string>
//{
//    ["ClassA"] = "ClassA",  // Migrate ClassA to ClassA (same name)
//    ["OriginalClass"] = "RenamedClass",  // Migrate to different class name
//    ["Utility"] = "CommonUtilities"  // Another migration
//};

//patcher.MigrateClassesInSolution(
//    solutionPath: @"C:\MySolution.sln",
//    classMigrationMap: classMigrationMap);

public class ClassMigrationPatcher
{
    public void MigrateClass(string sourceFilePath, string destinationFilePath, string className)
    {
        // Read and parse both files
        var sourceContent = File.ReadAllText(sourceFilePath);
        var destinationContent = File.ReadAllText(destinationFilePath);

        var sourceTree = CSharpSyntaxTree.ParseText(sourceContent);
        var destinationTree = CSharpSyntaxTree.ParseText(destinationContent);

        var sourceRoot = sourceTree.GetRoot();
        var destinationRoot = destinationTree.GetRoot();

        // Find the class to migrate in source
        var sourceClass = sourceRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);

        if (sourceClass == null)
        {
            throw new InvalidOperationException($"Class {className} not found in source file");
        }

        // Find the corresponding class in destination
        var destinationClass = destinationRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);

        if (destinationClass == null)
        {
            throw new InvalidOperationException($"Class {className} not found in destination file");
        }

        // Replace the class in destination with the one from source
        var newDestinationRoot = destinationRoot.ReplaceNode(destinationClass, sourceClass);

        // Preserve original file formatting and comments
        newDestinationRoot = FormatPreservingRewriter.PreserveFormatting(destinationRoot, newDestinationRoot);

        // Write the modified file
        File.WriteAllText(destinationFilePath, newDestinationRoot.ToFullString());
    }

    public void MigrateClassesInSolution(string solutionPath, Dictionary<string, string> classMigrationMap)
    {
        var solution = SolutionFile.Parse(solutionPath);
        var fileClassMap = BuildFileClassMap(solution);

        foreach (var migration in classMigrationMap)
        {
            var sourceClassName = migration.Key;
            var destinationClassName = migration.Value;

            if (!fileClassMap.TryGetValue(sourceClassName, out var sourceFile))
            {
                Console.WriteLine($"Warning: Source class {sourceClassName} not found in solution");
                continue;
            }

            if (!fileClassMap.TryGetValue(destinationClassName, out var destinationFile))
            {
                Console.WriteLine($"Warning: Destination class {destinationClassName} not found in solution");
                continue;
            }

            try
            {
                MigrateClass(sourceFile.FilePath, destinationFile.FilePath, sourceClassName);
                Console.WriteLine($"Successfully migrated {sourceClassName} from {sourceFile.FilePath} to {destinationFile.FilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error migrating {sourceClassName}: {ex.Message}");
            }
        }
    }

    private Dictionary<string, (string FilePath, ClassDeclarationSyntax Syntax)> BuildFileClassMap(SolutionFile solution)
    {
        var map = new Dictionary<string, (string, ClassDeclarationSyntax)>();

        foreach (var project in solution.ProjectsInOrder)
        {
            if (project.ProjectType != SolutionProjectType.KnownToBeMSBuildFormat) continue;

            var projectDir = Path.GetDirectoryName(project.AbsolutePath);
            var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories);

            foreach (var filePath in csFiles)
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    var tree = CSharpSyntaxTree.ParseText(content);
                    var root = tree.GetRoot();

                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                    foreach (var classDecl in classes)
                    {
                        var className = classDecl.Identifier.Text;
                        if (map.ContainsKey(className))
                        {
                            Console.WriteLine($"Warning: Duplicate class name {className} found in {filePath}");
                            continue;
                        }

                        map[className] = (filePath, classDecl);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
                }
            }
        }

        return map;
    }
}
