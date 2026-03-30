using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Conjecture.Generators;

internal enum ConstructionMode { Constructor, ObjectInitializer }

internal enum MemberGenerationKind { Primitive, Enum, NullableValue, List, ArbitraryReference, Unsupported }

internal sealed record TypeModel(
    string FullyQualifiedName,
    string Namespace,
    string TypeName,
    TypeKind TypeKind,
    ImmutableArray<string> TypeParameters,
    ImmutableArray<MemberModel> Members,
    ConstructionMode ConstructionMode = ConstructionMode.Constructor);

internal sealed record MemberModel(
    string Name,
    string TypeFullName,
    bool IsNullable,
    MemberGenerationKind Kind = MemberGenerationKind.Primitive,
    string InnerTypeFullName = "");
