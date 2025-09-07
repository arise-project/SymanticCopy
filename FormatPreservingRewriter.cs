using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Build.Construction;

namespace SymanticCopy;

public static class FormatPreservingRewriter
{
    public static SyntaxNode PreserveFormatting(
    	SyntaxNode originalRoot, 
    	SyntaxNode newRoot)
    {
        var rewriter = new FormatPreservingSyntaxRewriter(originalRoot);

        return rewriter.Visit(newRoot);
    }

    private class FormatPreservingSyntaxRewriter 
    	: CSharpSyntaxRewriter
    {
        private readonly SyntaxNode _originalRoot;

        public FormatPreservingSyntaxRewriter(SyntaxNode originalRoot)
        {
            _originalRoot = originalRoot;
        }

        public override SyntaxTriviaList VisitList(SyntaxTriviaList list)
        {
            // Try to find matching trivia from original file
            if (_originalRoot != null)
            {
                var originalTrivia = _originalRoot.DescendantTrivia()
                    .FirstOrDefault(t => t.IsEquivalentTo(list.FirstOrDefault()));

                if (originalTrivia != default)
                {
                    return originalTrivia.Token.LeadingTrivia;
                }
            }

            return base.VisitList(list);
        }
    }
}
