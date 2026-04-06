// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class LabeledStrategy<T>(Strategy<T> inner, string label) : Strategy<T>(label)
{
    internal override T Generate(ConjectureData data) => inner.Generate(data);
}