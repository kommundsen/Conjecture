using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Conjecture.Generators;

internal static class TypeModelExtractor
{
    private static readonly SymbolDisplayFormat TypeNameFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

    private static readonly DiagnosticDescriptor Hyp200 = new(
        id: "HYP200",
        title: "No accessible constructor",
        messageFormat: "Type '{0}' has no accessible constructor; cannot emit strategy",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Hyp201 = new(
        id: "HYP201",
        title: "Type is not partial",
        messageFormat: "Type '{0}' decorated with [Arbitrary] must be partial",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static (TypeModel? Model, Diagnostic? Diagnostic) Extract(INamedTypeSymbol symbol)
    {
        Location location = symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None;

        if (!IsPartial(symbol))
        {
            return (null, Diagnostic.Create(Hyp201, location, symbol.Name));
        }

        IMethodSymbol? bestCtor = FindBestConstructor(symbol);

        string ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();

        ImmutableArray<string> typeParameters = BuildTypeParameters(symbol);

        ImmutableArray<MemberModel> members;
        ConstructionMode mode;

        if (bestCtor is not null)
        {
            members = BuildMembers(bestCtor);
            mode = ConstructionMode.Constructor;
        }
        else
        {
            members = BuildInitPropertyMembers(symbol);
            if (members.IsEmpty)
            {
                return (null, Diagnostic.Create(Hyp200, location, symbol.Name));
            }

            mode = ConstructionMode.ObjectInitializer;
        }

        TypeModel model = new(
            FullyQualifiedName: symbol.ToDisplayString(TypeNameFormat),
            Namespace: ns,
            TypeName: symbol.Name,
            TypeKind: symbol.TypeKind,
            TypeParameters: typeParameters,
            Members: members,
            ConstructionMode: mode);

        return (model, null);
    }

    private static bool IsPartial(INamedTypeSymbol symbol)
    {
        foreach (SyntaxReference syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDecl)
            {
                foreach (SyntaxToken modifier in typeDecl.Modifiers)
                {
                    if (modifier.IsKind(SyntaxKind.PartialKeyword))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static IMethodSymbol? FindBestConstructor(INamedTypeSymbol symbol)
    {
        IMethodSymbol? best = null;
        foreach (IMethodSymbol ctor in symbol.InstanceConstructors)
        {
            if (ctor.IsImplicitlyDeclared || ctor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            if (best is null || ctor.Parameters.Length > best.Parameters.Length)
            {
                best = ctor;
            }
        }

        return best;
    }

    private static ImmutableArray<string> BuildTypeParameters(INamedTypeSymbol symbol)
    {
        if (symbol.TypeParameters.IsEmpty)
        {
            return ImmutableArray<string>.Empty;
        }

        ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>(symbol.TypeParameters.Length);
        foreach (ITypeParameterSymbol tp in symbol.TypeParameters)
        {
            builder.Add(tp.Name);
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<MemberModel> BuildMembers(IMethodSymbol ctor)
    {
        if (ctor.Parameters.IsEmpty)
        {
            return ImmutableArray<MemberModel>.Empty;
        }

        ImmutableArray<MemberModel>.Builder builder = ImmutableArray.CreateBuilder<MemberModel>(ctor.Parameters.Length);
        foreach (IParameterSymbol param in ctor.Parameters)
        {
            string typeName = param.Type.ToDisplayString(TypeNameFormat);
            bool isNullable = param.NullableAnnotation == NullableAnnotation.Annotated;
            builder.Add(new(param.Name, typeName, isNullable));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<MemberModel> BuildInitPropertyMembers(INamedTypeSymbol symbol)
    {
        ImmutableArray<MemberModel>.Builder builder = ImmutableArray.CreateBuilder<MemberModel>();
        foreach (ISymbol member in symbol.GetMembers())
        {
            if (member is not IPropertySymbol prop)
            {
                continue;
            }

            if (prop.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            if (prop.SetMethod is null || !prop.SetMethod.IsInitOnly)
            {
                continue;
            }

            string typeName = prop.Type.ToDisplayString(TypeNameFormat);
            bool isNullable = prop.NullableAnnotation == NullableAnnotation.Annotated;
            builder.Add(new(prop.Name, typeName, isNullable));
        }

        return builder.ToImmutable();
    }
}
