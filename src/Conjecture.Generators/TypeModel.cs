// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Conjecture.Generators;

internal enum ConstructionMode { Constructor, ObjectInitializer, PartialConstructor }

internal enum MemberGenerationKind { Primitive, Enum, NullableValue, List, ArbitraryReference, ExternalStrategyProvider, Unsupported, Dictionary, ImmutableArray, Set, ValueTuple, Recursive }

internal sealed record TypeModel(
    string FullyQualifiedName,
    string Namespace,
    string TypeName,
    TypeKind TypeKind,
    ImmutableArray<string> TypeParameters,
    ImmutableArray<MemberModel> Members,
    ConstructionMode ConstructionMode = ConstructionMode.Constructor,
    int MaxDepth = 5,
    bool IsPartial = true);

internal sealed record MemberModel(
    string Name,
    string TypeFullName,
    bool IsNullable,
    MemberGenerationKind Kind = MemberGenerationKind.Primitive,
    string AuxiliaryTypeName = "",
    double? RangeMin = null,
    double? RangeMax = null,
    int? StringMinLength = null,
    int? StringMaxLength = null);