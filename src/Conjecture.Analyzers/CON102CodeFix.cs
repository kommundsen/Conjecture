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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Conjecture.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CON102CodeFix))]
[Shared]
internal sealed class CON102CodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(CON102Analyzer.Rule.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Convert to async/await",
                createChangedDocument: ct => ApplyFixAsync(context.Document, context.Diagnostics[0], ct),
                equivalenceKey: "CON102_ConvertToAsync"),
            context.Diagnostics[0]);
        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
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

        SyntaxNode? diagNode = root.FindNode(diagnostic.Location.SourceSpan);
        MethodDeclarationSyntax? method = diagNode?
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (method is null)
        {
            return document;
        }

        SyntaxNode? syncNode = null;
        ExpressionSyntax? awaitTarget = null;

        foreach (SyntaxNode node in method.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax getResultCall &&
                getResultCall.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "GetResult" } getResultAccess &&
                getResultAccess.Expression is InvocationExpressionSyntax getAwaiterCall &&
                getAwaiterCall.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "GetAwaiter" } getAwaiterAccess)
            {
                syncNode = getResultCall;
                awaitTarget = getAwaiterAccess.Expression;
                break;
            }

            if (node is InvocationExpressionSyntax waitCall &&
                waitCall.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Wait" } waitAccess &&
                CON102Analyzer.IsTaskType(semanticModel.GetTypeInfo(waitAccess.Expression).Type))
            {
                syncNode = waitCall;
                awaitTarget = waitAccess.Expression;
                break;
            }

            if (node is MemberAccessExpressionSyntax { Name.Identifier.Text: "Result" } resultAccess &&
                CON102Analyzer.IsTaskType(semanticModel.GetTypeInfo(resultAccess.Expression).Type))
            {
                syncNode = resultAccess;
                awaitTarget = resultAccess.Expression;
                break;
            }
        }

        if (syncNode is null || awaitTarget is null)
        {
            return document;
        }

        AwaitExpressionSyntax awaitExpression = SyntaxFactory
            .AwaitExpression(
                SyntaxFactory.Token(SyntaxKind.AwaitKeyword)
                    .WithTrailingTrivia(SyntaxFactory.Whitespace(" ")),
                awaitTarget.WithoutLeadingTrivia())
            .WithTriviaFrom(syncNode);

        // Replace sync node and apply async modifier in one tree rewrite
        MethodDeclarationSyntax newMethod = MakeAsync(
            (MethodDeclarationSyntax)method.ReplaceNode(syncNode, awaitExpression));
        SyntaxNode newRoot = root.ReplaceNode(method, newMethod);

        return document.WithSyntaxRoot(newRoot);
    }

    private static MethodDeclarationSyntax MakeAsync(MethodDeclarationSyntax method)
    {
        SyntaxToken asyncToken = SyntaxFactory
            .Token(SyntaxKind.AsyncKeyword)
            .WithTrailingTrivia(SyntaxFactory.Whitespace(" "));

        SyntaxTokenList modifiers = method.Modifiers.Add(asyncToken);

        bool isVoid = method.ReturnType is PredefinedTypeSyntax predefined &&
            predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

        TypeSyntax returnType = isVoid
            ? SyntaxFactory.IdentifierName("Task").WithTriviaFrom(method.ReturnType)
            : method.ReturnType;

        return method.WithModifiers(modifiers).WithReturnType(returnType);
    }
}