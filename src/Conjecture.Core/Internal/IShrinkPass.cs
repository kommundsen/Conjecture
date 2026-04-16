// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Internal;

internal interface IShrinkPass
{
    /// <summary>Gets the stable metric tag name for this pass (snake_case, never changes).</summary>
    string PassName { get; }

    /// <summary>Attempt one reduction step. Returns true if progress was made.</summary>
    ValueTask<bool> TryReduce(ShrinkState state);
}