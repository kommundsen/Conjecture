// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;

namespace Conjecture.Generators;

internal record HierarchyTypeModel(
    string FullyQualifiedName,
    string Namespace,
    string TypeName,
    ImmutableArray<string> TypeParameters,
    ImmutableArray<SubtypeModel> Subtypes);

internal record SubtypeModel(
    string FullyQualifiedName,
    string ProviderTypeName);