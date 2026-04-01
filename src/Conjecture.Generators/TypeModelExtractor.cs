// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
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

    internal static (TypeModel? Model, ImmutableArray<Diagnostic> Diagnostics) Extract(INamedTypeSymbol symbol)
    {
        Location location = symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None;

        if (!IsPartial(symbol))
        {
            return (null, ImmutableArray.Create(Diagnostic.Create(DiagnosticDescriptors.Con201, location, symbol.Name)));
        }

        IMethodSymbol? bestCtor = FindBestConstructor(symbol);

        string ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();

        ImmutableArray<string> typeParameters = BuildTypeParameters(symbol);

        List<Diagnostic> warnings = [];
        ImmutableArray<MemberModel> members;
        ConstructionMode mode;

        if (bestCtor is not null)
        {
            members = BuildMembers(bestCtor, warnings);
            mode = ConstructionMode.Constructor;
        }
        else
        {
            members = BuildInitPropertyMembers(symbol, warnings);
            if (members.IsEmpty)
            {
                return (null, ImmutableArray.Create(Diagnostic.Create(DiagnosticDescriptors.Con200, location, symbol.Name)));
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

        return (model, warnings.ToImmutableArray());
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

    private static ImmutableArray<MemberModel> BuildMembers(IMethodSymbol ctor, List<Diagnostic> warnings)
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
            (MemberGenerationKind kind, string innerFqn) = ClassifyMemberType(param.Type);

            if (kind == MemberGenerationKind.Unsupported)
            {
                Location loc = param.Locations.Length > 0 ? param.Locations[0] : Location.None;
                warnings.Add(Diagnostic.Create(DiagnosticDescriptors.Con202, loc, param.Name, typeName));
            }

            builder.Add(new(param.Name, typeName, isNullable, kind, innerFqn));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<MemberModel> BuildInitPropertyMembers(INamedTypeSymbol symbol, List<Diagnostic> warnings)
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
            (MemberGenerationKind kind, string innerFqn) = ClassifyMemberType(prop.Type);

            if (kind == MemberGenerationKind.Unsupported)
            {
                Location loc = prop.Locations.Length > 0 ? prop.Locations[0] : Location.None;
                warnings.Add(Diagnostic.Create(DiagnosticDescriptors.Con202, loc, prop.Name, typeName));
            }

            builder.Add(new(prop.Name, typeName, isNullable, kind, innerFqn));
        }

        return builder.ToImmutable();
    }

    private static (MemberGenerationKind Kind, string InnerFqn) ClassifyMemberType(ITypeSymbol type)
    {
        if (IsPrimitive(type.SpecialType))
        {
            return (MemberGenerationKind.Primitive, "");
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return (MemberGenerationKind.Enum, "");
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType)
        {
            string innerFqn = nullableType.TypeArguments[0].ToDisplayString(TypeNameFormat);
            return (MemberGenerationKind.NullableValue, innerFqn);
        }

        if (type is INamedTypeSymbol { Name: "List", TypeArguments.Length: 1 } listType
            && listType.ContainingNamespace is
            {
                Name: "Generic",
                ContainingNamespace.Name: "Collections",
                ContainingNamespace.ContainingNamespace.Name: "System"
            })
        {
            string innerFqn = listType.TypeArguments[0].ToDisplayString(TypeNameFormat);
            return (MemberGenerationKind.List, innerFqn);
        }

        foreach (AttributeData attr in type.GetAttributes())
        {
            if (attr.AttributeClass is
                {
                    Name: "ArbitraryAttribute",
                    ContainingNamespace.Name: "Core",
                    ContainingNamespace.ContainingNamespace.Name: "Conjecture"
                })
            {
                return (MemberGenerationKind.ArbitraryReference, "");
            }
        }

        return (MemberGenerationKind.Unsupported, "");
    }

    private static bool IsPrimitive(SpecialType st)
    {
        return st is SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Byte
            or SpecialType.System_Boolean or SpecialType.System_String
            or SpecialType.System_Double or SpecialType.System_Single;
    }
}