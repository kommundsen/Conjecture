// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Conjecture.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CON103CodeFix))]
[Shared]
internal sealed class CON103CodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(CON103Analyzer.Rule.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Swap min and max arguments",
                createChangedDocument: ct => SwapArgsAsync(context.Document, context.Diagnostics[0], ct),
                equivalenceKey: "CON103_SwapArgs"),
            context.Diagnostics[0]);
        return Task.CompletedTask;
    }

    private static async Task<Document> SwapArgsAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken);
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken);

        if (root is null || semanticModel is null)
        {
            return document;
        }

        InvocationExpressionSyntax? invocation =
            root.FindNode(diagnostic.Location.SourceSpan)
                .AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault();

        if (invocation is null)
        {
            return document;
        }

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return document;
        }

        if (!GenBoundsHelper.TryFindMinMaxArgIndices(invocation, method, out int minArgPos, out int maxArgPos))
        {
            return document;
        }

        SeparatedSyntaxList<ArgumentSyntax> args = invocation.ArgumentList.Arguments;
        ArgumentSyntax minArg = args[minArgPos];
        ArgumentSyntax maxArg = args[maxArgPos];

        ArgumentSyntax newMinArg = minArg.WithExpression(
            maxArg.Expression.WithTriviaFrom(minArg.Expression));
        ArgumentSyntax newMaxArg = maxArg.WithExpression(
            minArg.Expression.WithTriviaFrom(maxArg.Expression));

        SyntaxNode newRoot = root.ReplaceNodes(
            new[] { minArg, maxArg },
            (original, _) =>
                original == minArg ? newMinArg : original == maxArg ? newMaxArg : original);

        return document.WithSyntaxRoot(newRoot);
    }
}