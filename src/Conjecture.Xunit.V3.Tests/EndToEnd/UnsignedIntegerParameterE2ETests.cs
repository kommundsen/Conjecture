// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Xunit.V3.Tests.EndToEnd;

/// <summary>
/// End-to-end tests proving the [Property] resolver path resolves every
/// IBinaryInteger&lt;T&gt; type — both the explicit fast-paths and the reflective
/// fallback for native-sized integers.
/// </summary>
public sealed class UnsignedIntegerParameterE2ETests
{
    [Property(MaxExamples = 20, Seed = 1UL)]
    public void UInt_Param_Resolves(uint x) { _ = x; }

    [Property(MaxExamples = 20, Seed = 2UL)]
    public void ULong_Param_Resolves(ulong x) { _ = x; }

    [Property(MaxExamples = 20, Seed = 3UL)]
    public void UShort_Param_Resolves(ushort x) { _ = x; }

    [Property(MaxExamples = 20, Seed = 4UL)]
    public void SByte_Param_Resolves(sbyte x) { _ = x; }

    [Property(MaxExamples = 20, Seed = 5UL)]
    public void Short_Param_Resolves(short x) { _ = x; }

    [Property(MaxExamples = 20, Seed = 6UL)]
    public void NInt_Param_Resolves(nint x) { _ = x; }

    [Property(MaxExamples = 20, Seed = 7UL)]
    public void NUInt_Param_Resolves(nuint x) { _ = x; }

    [Property(MaxExamples = 20, Seed = 8UL)]
    public void Mixed_UnsignedExoticIntegers_Resolve(uint a, ulong b, ushort c, sbyte d, short e)
    {
        _ = a; _ = b; _ = c; _ = d; _ = e;
    }
}
