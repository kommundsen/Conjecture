// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/


namespace Conjecture.Core;

/// <summary>Formats a counterexample value of type <typeparamref name="T"/> for display in failure messages.</summary>
/// <typeparam name="T">The value type this formatter handles.</typeparam>
public interface IStrategyFormatter<T>
{
    /// <summary>Returns a human-readable string representation of <paramref name="value"/>.</summary>
    string Format(T value);
}