using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace SymanticCopy;

public class DotNetProjectParser
{
    public Dictionary<string, string> ParseFile(string filePath)
    {
        var classDictionary = new Dictionary<string, string>();
        var fileContent = File.ReadAllText(filePath);

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(fileContent);
            var root = syntaxTree.GetRoot();

            // Get all class, struct, record, and interface declarations
            var typeDeclarations = root.DescendantNodes()
                .Where(n => n is ClassDeclarationSyntax
                         || n is StructDeclarationSyntax
                         || n is RecordDeclarationSyntax
                         || n is InterfaceDeclarationSyntax);

            foreach (var typeDecl in typeDeclarations)
            {
                var normalizedContent = NormalizeTypeContent(typeDecl);
                var typeName = GetTypeName(typeDecl);

                classDictionary[typeName] = normalizedContent;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing file {filePath}: {ex.Message}");
        }

        return classDictionary;
    }

    private string GetTypeName(SyntaxNode typeDecl)
    {
        return typeDecl switch
        {
            ClassDeclarationSyntax classDecl => classDecl.Identifier.Text,
            StructDeclarationSyntax structDecl => structDecl.Identifier.Text,
            RecordDeclarationSyntax recordDecl => recordDecl.Identifier.Text,
            InterfaceDeclarationSyntax interfaceDecl => interfaceDecl.Identifier.Text,
            _ => throw new NotSupportedException($"Unsupported type: {typeDecl.GetType().Name}")
        };
    }

    private string NormalizeTypeContent(SyntaxNode typeDecl)
    {
        // Remove all trivia (whitespace, comments, etc.)
        var normalized = typeDecl
            .WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString();

        // Additional normalization steps:
        // 1. Remove attributes
        // 2. Standardize line endings
        // 3. Sort members?

        return normalized;
    }

    public Dictionary<string, string> ParseProject(string projectDirectory)
    {
        var classDictionary = new Dictionary<string, string>();

        // Get all .cs files in the project directory
        var csFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);

        foreach (var filePath in csFiles)
        {
            try
            {
                var fileContent = File.ReadAllText(filePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(fileContent);
                var root = syntaxTree.GetRoot();

                // Get all class declarations
                var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classDecl in classDeclarations)
                {
                    // Normalize the class content (removing formatting, comments, etc.)
                    var normalizedClass = NormalizeClassContent(classDecl);

                    // Use the class name as key (without namespace)
                    var className = classDecl.Identifier.Text;

                    // Handle potential duplicate class names (unlikely in same project)
                    if (classDictionary.ContainsKey(className))
                    {
                        className = $"{className}_{Guid.NewGuid().ToString("N").Substring(0, 4)}";
                    }

                    classDictionary[className] = normalizedClass;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
            }
        }

        return classDictionary;
    }

    private string NormalizeClassContent(ClassDeclarationSyntax classDecl)
    {
        // Remove all trivia (whitespace, comments, etc.)
        var normalized = classDecl
            .WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString();

        // Further normalization steps:
        // 1. Remove access modifiers (public, private, etc.)
        // 2. Standardize method bodies
        // 3. Sort members alphabetically?

        // For now, just return the syntax-normalized version
        return normalized;
    }

    public void SaveToCsv(Dictionary<string, string> classDictionary, string outputPath)
    {
        var csvContent = new StringBuilder();
        csvContent.AppendLine("ClassName,ClassContent");

        foreach (var kvp in classDictionary)
        {
            // Escape quotes and newlines in the class content
            var escapedContent = kvp.Value.Replace("\"", "\"\"")
                                         .Replace("\r", "\\r")
                                         .Replace("\n", "\\n");
            csvContent.AppendLine($"\"{kvp.Key}\",\"{escapedContent}\"");
        }

        File.WriteAllText(outputPath, csvContent.ToString());
    }

    public Dictionary<string, string> LoadFromCsv(string inputPath)
    {
        var classDictionary = new Dictionary<string, string>();

        var lines = File.ReadAllLines(inputPath).Skip(1); // Skip header

        foreach (var line in lines)
        {
            var parts = line.Split(new[] { "\",\"" }, StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                var className = parts[0].TrimStart('"');
                var classContent = parts[1].TrimEnd('"')
                                          .Replace("\"\"", "\"")
                                          .Replace("\\r", "\r")
                                          .Replace("\\n", "\n");

                classDictionary[className] = classContent;
            }
        }

        return classDictionary;
    }
}