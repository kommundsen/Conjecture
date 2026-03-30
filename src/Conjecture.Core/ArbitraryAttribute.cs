namespace Conjecture.Core;

/// <summary>Marks a type for automatic <c>IStrategyProvider</c> derivation by the source generator.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class ArbitraryAttribute : Attribute { }
