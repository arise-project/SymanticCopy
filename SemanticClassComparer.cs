using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Security.Cryptography;


namespace SymanticCopy;

//// Example usage:
//var comparer = new SemanticClassComparer();

//// Class content from your parsed dictionaries
//var class1Content = @"
//public class Calculator
//{
//    public int Add(int a, int b) 
//    {
//        return a + b;
//    }
    
//    private double Sqrt(double x) => Math.Sqrt(x);
//}";

//var class2Content = @"
//public class Calculator
//{
//    public int Add(int a, int b)
//    {
//        // Different implementation
//        int sum = a;
//        sum += b;
//        return sum;
//    }
    
//    protected double SquareRoot(double x) => Math.Sqrt(x);
//}";

//var result = comparer.CompareClasses(class1Content, class2Content);

//Console.WriteLine($"Signatures equal: {result.AreSignaturesEqual}");
//Console.WriteLine($"Implementations equal: {result.AreImplementationsSemanticallyEqual}");
//Console.WriteLine($"Similarity score: {result.SimilarityScore:P0}");

//foreach (var diff in result.MemberDifferences)
//{
//    Console.WriteLine($"{diff.Type}: {diff.Description}");
//}

public class SemanticClassComparer
{
    private readonly HashAlgorithm _hashAlgorithm = SHA256.Create();

    public class ComparisonResult
    {
        public bool AreSignaturesEqual { get; set; }
        public bool AreImplementationsSemanticallyEqual { get; set; }
        public double SimilarityScore { get; set; }
        public List<MemberDifference> MemberDifferences { get; } = new List<MemberDifference>();
        public List<string> Warnings { get; } = new List<string>();
    }

    public class MemberDifference
    {
        public string MemberName { get; set; }
        public DifferenceType Type { get; set; }
        public string Description { get; set; }
    }

    public enum DifferenceType
    {
        SignatureChange,
        ImplementationChange,
        AddedMember,
        RemovedMember,
        AccessibilityChange,
        ModifierChange
    }

    public ComparisonResult CompareClasses(string class1Content, string class2Content)
    {
        var result = new ComparisonResult();

        // Parse both classes
        var syntaxTree1 = CSharpSyntaxTree.ParseText(class1Content);
        var syntaxTree2 = CSharpSyntaxTree.ParseText(class2Content);

        var class1 = syntaxTree1.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var class2 = syntaxTree2.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

        if (class1 == null || class2 == null)
        {
            result.Warnings.Add("One or both inputs are not valid class declarations");
            return result;
        }

        // Compare signatures (structure)
        var signatureComparison = CompareSignatures(class1, class2);
        result.AreSignaturesEqual = signatureComparison.AreEqual;
        result.MemberDifferences.AddRange(signatureComparison.Differences);

        // Compare implementations (semantics)
        var semanticComparison = CompareImplementations(class1, class2);
        result.AreImplementationsSemanticallyEqual = semanticComparison.AreEqual;
        result.SimilarityScore = semanticComparison.SimilarityScore;
        result.MemberDifferences.AddRange(semanticComparison.Differences);

        return result;
    }

    private SignatureComparisonResult CompareSignatures(ClassDeclarationSyntax class1, ClassDeclarationSyntax class2)
    {
        var result = new SignatureComparisonResult();
        var members1 = GetMembers(class1);
        var members2 = GetMembers(class2);

        // Check for added/removed members
        var memberNames1 = members1.Select(m => m.Key).ToHashSet();
        var memberNames2 = members2.Select(m => m.Key).ToHashSet();

        foreach (var addedMember in memberNames2.Except(memberNames1))
        {
            result.Differences.Add(new MemberDifference
            {
                MemberName = addedMember,
                Type = DifferenceType.AddedMember,
                Description = $"Member '{addedMember}' was added"
            });
        }

        foreach (var removedMember in memberNames1.Except(memberNames2))
        {
            result.Differences.Add(new MemberDifference
            {
                MemberName = removedMember,
                Type = DifferenceType.RemovedMember,
                Description = $"Member '{removedMember}' was removed"
            });
        }

        // Compare common members
        foreach (var memberName in memberNames1.Intersect(memberNames2))
        {
            var member1 = members1[memberName];
            var member2 = members2[memberName];

            // Compare member signatures
            var signature1 = GetMemberSignature(member1);
            var signature2 = GetMemberSignature(member2);

            if (signature1 != signature2)
            {
                result.Differences.Add(new MemberDifference
                {
                    MemberName = memberName,
                    Type = DifferenceType.SignatureChange,
                    Description = $"Signature changed from '{signature1}' to '{signature2}'"
                });
            }

            // Compare accessibility
            var accessibility1 = GetAccessibility(member1);
            var accessibility2 = GetAccessibility(member2);

            if (accessibility1 != accessibility2)
            {
                result.Differences.Add(new MemberDifference
                {
                    MemberName = memberName,
                    Type = DifferenceType.AccessibilityChange,
                    Description = $"Accessibility changed from {accessibility1} to {accessibility2}"
                });
            }

            // Compare modifiers
            var modifiers1 = GetModifiers(member1);
            var modifiers2 = GetModifiers(member2);

            if (!modifiers1.SequenceEqual(modifiers2))
            {
                result.Differences.Add(new MemberDifference
                {
                    MemberName = memberName,
                    Type = DifferenceType.ModifierChange,
                    Description = $"Modifiers changed from [{string.Join(", ", modifiers1)}] to [{string.Join(", ", modifiers2)}]"
                });
            }
        }

        result.AreEqual = result.Differences.Count == 0;
        return result;
    }

    private SemanticComparisonResult CompareImplementations(ClassDeclarationSyntax class1, ClassDeclarationSyntax class2)
    {
        var result = new SemanticComparisonResult();
        var members1 = GetMembers(class1);
        var members2 = GetMembers(class2);

        var commonMembers = members1.Keys.Intersect(members2.Keys).ToList();
        if (commonMembers.Count == 0)
        {
            result.AreEqual = false;
            result.SimilarityScore = 0;
            return result;
        }

        int matchingMembers = 0;
        double totalSimilarity = 0;

        foreach (var memberName in commonMembers)
        {
            var member1 = members1[memberName];
            var member2 = members2[memberName];

            // Skip abstract/extern members without implementation
            if (IsAbstractOrExtern(member1) continue;

            var impl1 = GetNormalizedImplementation(member1);
            var impl2 = GetNormalizedImplementation(member2);

            // Compare hash of normalized implementations
            var hash1 = GetSemanticHash(impl1);
            var hash2 = GetSemanticHash(impl2);

            if (hash1 == hash2)
            {
                matchingMembers++;
                totalSimilarity += 1.0;
            }
            else
            {
                // Calculate similarity score for different implementations
                var similarity = CalculateCodeSimilarity(impl1, impl2);
                totalSimilarity += similarity;

                if (similarity < 0.95) // Threshold for considering different
                {
                    result.Differences.Add(new MemberDifference
                    {
                        MemberName = memberName,
                        Type = DifferenceType.ImplementationChange,
                        Description = $"Implementation changed (similarity: {similarity:P0})"
                    });
                }
            }
        }

        result.AreEqual = matchingMembers == commonMembers.Count;
        result.SimilarityScore = commonMembers.Count > 0 ? totalSimilarity / commonMembers.Count : 0;
        return result;
    }

    private double CalculateCodeSimilarity(string code1, string code2)
    {
        // Simple token-based similarity (can be enhanced with more advanced techniques)
        var tokens1 = GetCodeTokens(code1);
        var tokens2 = GetCodeTokens(code2);

        var commonTokens = tokens1.Intersect(tokens2).Count();
        var totalTokens = tokens1.Union(tokens2).Count();

        return totalTokens > 0 ? (double)commonTokens / totalTokens : 1.0;
    }

    private IEnumerable<string> GetCodeTokens(string code)
    {
        // Simple tokenization - consider using Roslyn syntax tokens for better results
        return code.Split(new[] { ' ', '\t', '\r', '\n', '(', ')', '{', '}', ';', ',' },
                        StringSplitOptions.RemoveEmptyEntries)
                  .Where(t => t.Length > 1 && !IsKeyword(t));
    }

    private bool IsKeyword(string token)
    {
        // Basic C# keywords
        var keywords = new HashSet<string> { "if", "else", "for", "while", "do", "switch",
                                          "case", "return", "var", "class", "void", "int",
                                          "string", "bool", "true", "false", "null" };
        return keywords.Contains(token);
    }

    private string GetSemanticHash(string code)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(code);
        var hash = _hashAlgorithm.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private string GetNormalizedImplementation(MemberDeclarationSyntax member)
    {
        // Remove all trivia (comments, whitespace)
        var normalized = member.WithoutTrivia().NormalizeWhitespace().ToFullString();

        // Additional normalization steps:
        // 1. Standardize variable names (var1, var2, etc.)
        // 2. Remove compiler-generated attributes
        // 3. Normalize numeric literals

        return normalized;
    }

    private Dictionary<string, MemberDeclarationSyntax> GetMembers(ClassDeclarationSyntax classDecl)
    {
        var members = new Dictionary<string, MemberDeclarationSyntax>();

        foreach (var member in classDecl.Members)
        {
            string name = GetMemberName(member);
            if (!string.IsNullOrEmpty(name))
            {
                members[name] = member;
            }
        }

        return members;
    }

    private string GetMemberName(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax method => method.Identifier.Text,
            PropertyDeclarationSyntax prop => prop.Identifier.Text,
            FieldDeclarationSyntax field => field.Declaration.Variables.First().Identifier.Text,
            EventDeclarationSyntax evt => evt.Identifier.Text,
            _ => null
        };
    }

    private string GetMemberSignature(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax method => $"{method.ReturnType} {method.Identifier}{method.ParameterList}",
            PropertyDeclarationSyntax prop => $"{prop.Type} {prop.Identifier} {{ {(prop.AccessorList?.Accessors.ToString() ?? "")} }}",
            FieldDeclarationSyntax field => $"{field.Declaration.Type} {field.Declaration.Variables.First().Identifier}",
            EventDeclarationSyntax evt => $"event {evt.Type} {evt.Identifier}",
            _ => member.ToString()
        };
    }

    private string GetAccessibility(MemberDeclarationSyntax member)
    {
        var modifiers = member.Modifiers;
        return modifiers.FirstOrDefault(m => IsAccessModifier(m.ToString()))?.ToString() ?? "private";
    }

    private bool IsAccessModifier(string modifier)
    {
        return modifier is "public" or "private" or "protected" or "internal";
    }

    private IEnumerable<string> GetModifiers(MemberDeclarationSyntax member)
    {
        return member.Modifiers.Select(m => m.Text)
                     .Where(m => !IsAccessModifier(m));
    }

    private bool IsAbstractOrExtern(MemberDeclarationSyntax member)
    {
        return member.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword) ||
               member.Modifiers.Any(m => m.IsKind(SyntaxKind.ExternKeyword));
    }

    private class SignatureComparisonResult
    {
        public bool AreEqual { get; set; }
        public List<MemberDifference> Differences { get; } = new List<MemberDifference>();
    }

    private class SemanticComparisonResult
    {
        public bool AreEqual { get; set; }
        public double SimilarityScore { get; set; }
        public List<MemberDifference> Differences { get; } = new List<MemberDifference>();
    }
}