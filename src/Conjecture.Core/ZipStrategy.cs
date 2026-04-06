// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class ZipStrategy<TFirst, TSecond, TResult>(
    Strategy<TFirst> first,
    Strategy<TSecond> second,
    Func<TFirst, TSecond, TResult> resultSelector) : Strategy<TResult>
{
    internal override TResult Generate(ConjectureData data) =>
        resultSelector(first.Generate(data), second.Generate(data));
}