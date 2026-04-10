// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Conjecture.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CJ0050CodeFix))]
[Shared]
internal sealed class CJ0050CodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(CJ0050Analyzer.Rule.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace with named extension property",
                createChangedDocument: ct => ApplyFixAsync(context.Document, context.Diagnostics[0], ct),
                equivalenceKey: "CJ0050_ReplaceWithProperty"),
            context.Diagnostics[0]);
        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root is null)
        {
            return document;
        }

        if (!diagnostic.Properties.TryGetValue(CJ0050Analyzer.PropertyNameKey, out string? propertyName) ||
            propertyName is null)
        {
            return document;
        }

        SyntaxNode? diagNode = root.FindNode(diagnostic.Location.SourceSpan);
        InvocationExpressionSyntax? invocation = diagNode as InvocationExpressionSyntax
            ?? diagNode?.FirstAncestorOrSelf<InvocationExpressionSyntax>();

        if (invocation is null)
        {
            return document;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        MemberAccessExpressionSyntax replacement = SyntaxFactory
            .MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                memberAccess.Expression,
                SyntaxFactory.IdentifierName(propertyName))
            .WithTriviaFrom(invocation);

        SyntaxNode newRoot = root.ReplaceNode(invocation, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}