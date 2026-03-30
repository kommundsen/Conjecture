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
}
