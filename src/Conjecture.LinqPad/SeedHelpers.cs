// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.LinqPad;

internal static class SeedHelpers
{
    internal static ulong? ToUlong(int? seed) => seed.HasValue ? (ulong)seed.Value : null;
}