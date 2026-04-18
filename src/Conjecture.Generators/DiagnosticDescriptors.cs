// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;

namespace Conjecture.Generators;

internal static class DiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor Con200 = new(
        id: "CON200",
        title: "No accessible constructor",
        messageFormat: "Type '{0}' has no accessible constructor; cannot emit strategy",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Con201 = new(
        id: "CON201",
        title: "Type is not partial",
        messageFormat: "Type '{0}' decorated with [Arbitrary] must be partial",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Con202 = new(
        id: "CON202",
        title: "Unsupported member type",
        messageFormat: "Member '{0}' has unsupported type '{1}'; cannot auto-generate strategy. Use [From<T>] to provide one manually.",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Con203 = new(
        id: "CON203",
        title: "Multiple partial constructors",
        messageFormat: "[Arbitrary] type declares more than one partial constructor",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Con204 = new(
        id: "CON204",
        title: "Primary constructor combined with partial constructor",
        messageFormat: "[Arbitrary] type combines a primary constructor with a partial constructor declaration; use the standard path without a partial constructor",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Con205 = new(
        id: "CON205",
        title: "Concrete subtype excluded from sealed hierarchy strategy",
        messageFormat: "'{0}' is a concrete subtype of '{1}' but lacks [Arbitrary]; it will not be included in the generated OneOf strategy. Add [Arbitrary] to include it.",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Con300 = new(
        id: "CON300",
        title: "Base type must be abstract",
        messageFormat: "Type '{0}' must be abstract",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Con301 = new(
        id: "CON301",
        title: "Base type must be a class or record",
        messageFormat: "Type '{0}' must be a class or record, not {1}",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Con302 = new(
        id: "CON302",
        title: "No concrete subtypes found",
        messageFormat: "No concrete subtypes found for abstract type '{0}'",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}