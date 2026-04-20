// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Conjecture.Generators;

internal static class TypeModelExtractor
{
    internal static readonly SymbolDisplayFormat TypeNameFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

    internal static (TypeModel? Model, ImmutableArray<Diagnostic> Diagnostics) Extract(
        INamedTypeSymbol symbol,
        IReadOnlyDictionary<string, string>? providerRegistry = null)
    {
        Location location = symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None;

        if (!IsPartial(symbol))
        {
            return (null, ImmutableArray.Create(Diagnostic.Create(DiagnosticDescriptors.Con201, location, symbol.Name)));
        }

        List<ConstructorDeclarationSyntax> partialCtors = FindPartialConstructors(symbol);

        if (partialCtors.Count > 1)
        {
            return (null, ImmutableArray.Create(Diagnostic.Create(DiagnosticDescriptors.Con203, location)));
        }

        if (partialCtors.Count == 1 && HasPrimaryConstructor(symbol))
        {
            return (null, ImmutableArray.Create(Diagnostic.Create(DiagnosticDescriptors.Con204, location)));
        }

        IMethodSymbol? bestCtor = FindBestConstructor(symbol);

        string ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();

        ImmutableArray<string> typeParameters = BuildTypeParameters(symbol);

        List<Diagnostic> warnings = [];
        ImmutableArray<MemberModel> members;
        ConstructionMode mode;

        if (partialCtors.Count == 1)
        {
            members = BuildInitPropertyMembers(symbol, warnings, providerRegistry);
            mode = ConstructionMode.PartialConstructor;
        }
        else if (bestCtor is not null)
        {
            members = BuildMembers(bestCtor, symbol, warnings, providerRegistry);
            mode = ConstructionMode.Constructor;
        }
        else
        {
            members = BuildInitPropertyMembers(symbol, warnings, providerRegistry);
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

    private static bool HasModifier(SyntaxTokenList modifiers, SyntaxKind kind)
    {
        foreach (SyntaxToken modifier in modifiers)
        {
            if (modifier.IsKind(kind))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPartial(INamedTypeSymbol symbol)
    {
        foreach (SyntaxReference syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDecl)
            {
                if (HasModifier(typeDecl.Modifiers, SyntaxKind.PartialKeyword))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<ConstructorDeclarationSyntax> FindPartialConstructors(INamedTypeSymbol symbol)
    {
        List<ConstructorDeclarationSyntax> result = [];
        foreach (SyntaxReference syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDecl)
            {
                foreach (MemberDeclarationSyntax member in typeDecl.Members)
                {
                    if (member is ConstructorDeclarationSyntax ctorDecl
                        && HasModifier(ctorDecl.Modifiers, SyntaxKind.PartialKeyword))
                    {
                        result.Add(ctorDecl);
                    }
                }
            }
        }

        return result;
    }

    private static bool HasPrimaryConstructor(INamedTypeSymbol symbol)
    {
        foreach (SyntaxReference syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax { ParameterList: not null })
            {
                return true;
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

    private static ImmutableArray<MemberModel> BuildMembers(
        IMethodSymbol ctor,
        INamedTypeSymbol containingType,
        List<Diagnostic> warnings,
        IReadOnlyDictionary<string, string>? providerRegistry)
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
            (MemberGenerationKind kind, string innerFqn) = ClassifyMemberType(param.Type, providerRegistry);

            if (kind == MemberGenerationKind.Unsupported)
            {
                Location loc = param.Locations.Length > 0 ? param.Locations[0] : Location.None;
                warnings.Add(Diagnostic.Create(DiagnosticDescriptors.Con202, loc, param.Name, typeName));
            }

            (double? rMin, double? rMax, int? sMin, int? sMax, bool required) = ReadConstraints(param, containingType);
            bool effectiveNullable = isNullable && !(required && param.Type.IsReferenceType);

            builder.Add(new(param.Name, typeName, effectiveNullable, kind, innerFqn, rMin, rMax, sMin, sMax));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<MemberModel> BuildInitPropertyMembers(
        INamedTypeSymbol symbol,
        List<Diagnostic> warnings,
        IReadOnlyDictionary<string, string>? providerRegistry)
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
            (MemberGenerationKind kind, string innerFqn) = ClassifyMemberType(prop.Type, providerRegistry);

            if (kind == MemberGenerationKind.Unsupported)
            {
                Location loc = prop.Locations.Length > 0 ? prop.Locations[0] : Location.None;
                warnings.Add(Diagnostic.Create(DiagnosticDescriptors.Con202, loc, prop.Name, typeName));
            }

            (double? rMin, double? rMax, int? sMin, int? sMax, bool required) = ReadConstraints(prop);
            bool effectiveNullable = isNullable && !(required && prop.Type.IsReferenceType);

            builder.Add(new(prop.Name, typeName, effectiveNullable, kind, innerFqn, rMin, rMax, sMin, sMax));
        }

        return builder.ToImmutable();
    }

    private static (double? RangeMin, double? RangeMax, int? StringMinLength, int? StringMaxLength, bool Required)
        ReadConstraints(ISymbol memberSymbol, INamedTypeSymbol? containingType = null)
    {
        double? rangeMin = null;
        double? rangeMax = null;
        int? strMin = null;
        int? strMax = null;
        bool required = false;

        bool hasGenRange = false;
        bool hasGenStringLength = false;

        ApplyAttributeConstraints(
            memberSymbol.GetAttributes(),
            ref rangeMin, ref rangeMax, ref strMin, ref strMax, ref required,
            ref hasGenRange, ref hasGenStringLength);

        // For primary constructor parameters, also check the synthesized property —
        // some attributes targeting [property:] end up there.
        if (memberSymbol is IParameterSymbol paramSym && containingType is not null)
        {
            IPropertySymbol? synthProp = null;
            foreach (ISymbol member in containingType.GetMembers())
            {
                if (member is IPropertySymbol prop
                    && string.Equals(prop.Name, paramSym.Name, StringComparison.Ordinal))
                {
                    synthProp = prop;
                    break;
                }
            }

            if (synthProp is not null)
            {
                ApplyAttributeConstraints(
                    synthProp.GetAttributes(),
                    ref rangeMin, ref rangeMax, ref strMin, ref strMax, ref required,
                    ref hasGenRange, ref hasGenStringLength);
            }
        }

        return (rangeMin, rangeMax, strMin, strMax, required);
    }

    private static void ApplyAttributeConstraints(
        ImmutableArray<AttributeData> attrs,
        ref double? rangeMin, ref double? rangeMax,
        ref int? strMin, ref int? strMax,
        ref bool required,
        ref bool hasGenRange, ref bool hasGenStringLength)
    {
        foreach (AttributeData attr in attrs)
        {
            INamedTypeSymbol? attrClass = attr.AttributeClass;
            string? attrFqn = attrClass?.TypeKind == TypeKind.Error
                ? null
                : attrClass?.ToDisplayString();

            // For resolved attributes, match by FQN.
            // For unresolved (error) attributes, fall back to short-name syntax inspection.
            string shortName = GetAttributeShortName(attr);

            if (attrFqn == "Conjecture.Core.GenRangeAttribute")
            {
                if (!hasGenRange && attr.ConstructorArguments.Length >= 2)
                {
                    rangeMin = Convert.ToDouble(attr.ConstructorArguments[0].Value);
                    rangeMax = Convert.ToDouble(attr.ConstructorArguments[1].Value);
                    hasGenRange = true;
                }
            }
            else if (attrFqn == "Conjecture.Core.GenStringLengthAttribute")
            {
                if (!hasGenStringLength && attr.ConstructorArguments.Length >= 2)
                {
                    strMin = Convert.ToInt32(attr.ConstructorArguments[0].Value);
                    strMax = Convert.ToInt32(attr.ConstructorArguments[1].Value);
                    hasGenStringLength = true;
                }
            }
            else if (!hasGenRange
                && (attrFqn == "System.ComponentModel.DataAnnotations.RangeAttribute"
                    || (attrFqn is null && (shortName == "Range" || shortName == "RangeAttribute"))))
            {
                if (attr.ConstructorArguments.Length >= 2)
                {
                    TypedConstant arg0 = attr.ConstructorArguments[0];
                    TypedConstant arg1 = attr.ConstructorArguments[1];
                    if (arg0.Value is not null && arg1.Value is not null)
                    {
                        rangeMin = Convert.ToDouble(arg0.Value);
                        rangeMax = Convert.ToDouble(arg1.Value);
                    }
                }
                else if (attrFqn is null)
                {
                    TryReadRangeFromSyntax(attr, ref rangeMin, ref rangeMax);
                }
            }
            else if (!hasGenStringLength
                && (attrFqn == "System.ComponentModel.DataAnnotations.StringLengthAttribute"
                    || (attrFqn is null && (shortName == "StringLength" || shortName == "StringLengthAttribute"))))
            {
                if (attr.ConstructorArguments.Length >= 1)
                {
                    strMax = Convert.ToInt32(attr.ConstructorArguments[0].Value);
                    strMin = 0;
                    foreach (KeyValuePair<string, TypedConstant> named in attr.NamedArguments)
                    {
                        if (named.Key == "MinimumLength" && named.Value.Value is not null)
                        {
                            strMin = Convert.ToInt32(named.Value.Value);
                        }
                    }

                    hasGenStringLength = true;
                }
                else if (attrFqn is null)
                {
                    TryReadStringLengthFromSyntax(attr, ref strMin, ref strMax);
                    hasGenStringLength = strMax.HasValue;
                }
            }
            else if (attrFqn == "System.ComponentModel.DataAnnotations.RequiredAttribute"
                || (attrFqn is null && (shortName == "Required" || shortName == "RequiredAttribute")))
            {
                required = true;
            }
        }
    }

    private static string GetAttributeShortName(AttributeData attr)
    {
        return attr.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax syntax
            ? syntax.Name switch
            {
                SimpleNameSyntax simple => simple.Identifier.Text,
                QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
                _ => string.Empty,
            }
            : attr.AttributeClass?.Name ?? string.Empty;
    }

    private static void TryReadStringLengthFromSyntax(AttributeData attr, ref int? strMin, ref int? strMax)
    {
        if (attr.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax syntax
            || syntax.ArgumentList is null)
        {
            return;
        }

        AttributeArgumentSyntax[] args = [.. syntax.ArgumentList.Arguments];
        int posIdx = 0;
        foreach (AttributeArgumentSyntax arg in args)
        {
            if (arg.NameEquals is not null)
            {
                // Named argument
                string argName = arg.NameEquals.Name.Identifier.Text;
                if (argName == "MinimumLength" && TryParseIntLiteral(arg.Expression, out int minVal))
                {
                    strMin = minVal;
                }
            }
            else
            {
                // Positional: first is maxLength
                if (posIdx == 0 && TryParseIntLiteral(arg.Expression, out int maxVal))
                {
                    strMax = maxVal;
                    strMin ??= 0;
                }

                posIdx++;
            }
        }
    }

    private static void TryReadRangeFromSyntax(AttributeData attr, ref double? rangeMin, ref double? rangeMax)
    {
        if (attr.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax syntax
            || syntax.ArgumentList is null)
        {
            return;
        }

        AttributeArgumentSyntax[] args = [.. syntax.ArgumentList.Arguments];
        if (args.Length >= 2
            && TryParseDoubleLiteral(args[0].Expression, out double minVal)
            && TryParseDoubleLiteral(args[1].Expression, out double maxVal))
        {
            rangeMin = minVal;
            rangeMax = maxVal;
        }
    }

    private static bool TryParseIntLiteral(ExpressionSyntax expr, out int value)
    {
        if (expr is LiteralExpressionSyntax { Token.Value: int intVal })
        {
            value = intVal;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryParseDoubleLiteral(ExpressionSyntax expr, out double value)
    {
        if (expr is LiteralExpressionSyntax literal)
        {
            if (literal.Token.Value is double dVal)
            {
                value = dVal;
                return true;
            }

            if (literal.Token.Value is int iVal)
            {
                value = iVal;
                return true;
            }

            if (literal.Token.Value is float fVal)
            {
                value = fVal;
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static readonly HashSet<string> KnownNonSpecialPrimitives =
    [
        "System.Guid",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.DateOnly",
        "System.TimeOnly",
        "System.TimeSpan",
    ];

    private static (MemberGenerationKind Kind, string InnerFqn) ClassifyMemberType(
        ITypeSymbol type,
        IReadOnlyDictionary<string, string>? providerRegistry)
    {
        if (IsPrimitive(type.SpecialType))
        {
            return (MemberGenerationKind.Primitive, "");
        }

        string typeFqn = type.ToDisplayString(TypeNameFormat);

        if (KnownNonSpecialPrimitives.Contains(typeFqn))
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

        if (type is INamedTypeSymbol { IsTupleType: true } tupleType
            && tupleType.TypeArguments.Length is >= 2 and <= 4)
        {
            ImmutableArray<string>.Builder parts = ImmutableArray.CreateBuilder<string>(tupleType.TypeArguments.Length);
            foreach (ITypeSymbol arg in tupleType.TypeArguments)
            {
                parts.Add(arg.ToDisplayString(TypeNameFormat));
            }

            return (MemberGenerationKind.ValueTuple, string.Join("|", parts));
        }

        if (type is INamedTypeSymbol { TypeArguments.Length: 1 } singleArgType
            && IsInSystemCollectionsGeneric(singleArgType))
        {
            string innerFqn = singleArgType.TypeArguments[0].ToDisplayString(TypeNameFormat);
            return singleArgType.Name switch
            {
                "List" or "IReadOnlyList" or "IList" => (MemberGenerationKind.List, innerFqn),
                "HashSet" or "IReadOnlySet" => (MemberGenerationKind.Set, innerFqn),
                _ => (MemberGenerationKind.Unsupported, ""),
            };
        }

        if (type is INamedTypeSymbol { Name: "ImmutableArray", TypeArguments.Length: 1 } immArrType
            && IsInSystemCollectionsImmutable(immArrType))
        {
            string innerFqn = immArrType.TypeArguments[0].ToDisplayString(TypeNameFormat);
            return (MemberGenerationKind.ImmutableArray, innerFqn);
        }

        if (type is INamedTypeSymbol { Name: "Dictionary", TypeArguments.Length: 2 } dictType
            && IsInSystemCollectionsGeneric(dictType))
        {
            string keyFqn = dictType.TypeArguments[0].ToDisplayString(TypeNameFormat);
            string valFqn = dictType.TypeArguments[1].ToDisplayString(TypeNameFormat);
            return (MemberGenerationKind.Dictionary, keyFqn + "|" + valFqn);
        }

        if (type is INamedTypeSymbol namedType && SymbolHelpers.HasArbitraryAttribute(namedType))
        {
            return (MemberGenerationKind.ArbitraryReference, "");
        }

        if (providerRegistry is not null && providerRegistry.TryGetValue(typeFqn, out string? providerFqn))
        {
            return (MemberGenerationKind.ExternalStrategyProvider, providerFqn);
        }

        return (MemberGenerationKind.Unsupported, "");
    }

    private static bool IsInSystemCollectionsGeneric(INamedTypeSymbol type)
        => type.ContainingNamespace is
        {
            Name: "Generic",
            ContainingNamespace.Name: "Collections",
            ContainingNamespace.ContainingNamespace.Name: "System",
            ContainingNamespace.ContainingNamespace.ContainingNamespace.IsGlobalNamespace: true,
        };

    private static bool IsInSystemCollectionsImmutable(INamedTypeSymbol type)
        => type.ContainingNamespace is
        {
            Name: "Immutable",
            ContainingNamespace.Name: "Collections",
            ContainingNamespace.ContainingNamespace.Name: "System",
            ContainingNamespace.ContainingNamespace.ContainingNamespace.IsGlobalNamespace: true,
        };

    private static bool IsPrimitive(SpecialType st)
    {
        return st is SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Byte
            or SpecialType.System_Boolean or SpecialType.System_String
            or SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_Char
            or SpecialType.System_Decimal or SpecialType.System_UInt32 or SpecialType.System_UInt64
            or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_SByte;
    }
}