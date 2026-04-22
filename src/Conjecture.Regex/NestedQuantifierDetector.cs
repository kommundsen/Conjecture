// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.Regex;

internal static class NestedQuantifierDetector
{
    internal static bool HasNestedQuantifiers(RegexNode root)
    {
        return ContainsNestedQuantifier(root, insideQuantifier: false);
    }

    private static bool ContainsNestedQuantifier(RegexNode node, bool insideQuantifier)
    {
        return node switch
        {
            Quantifier q => insideQuantifier || ContainsNestedQuantifier(q.Inner, insideQuantifier: true),
            Alternation alt => insideQuantifier || ContainsNestedQuantifierInList(alt.Arms, insideQuantifier),
            Sequence seq => ContainsNestedQuantifierInList(seq.Items, insideQuantifier),
            Group grp => ContainsNestedQuantifier(grp.Inner, insideQuantifier),
            LookaroundAssertion la => ContainsNestedQuantifier(la.Inner, insideQuantifier),
            _ => false,
        };
    }

    private static bool ContainsNestedQuantifierInList(IReadOnlyList<RegexNode> items, bool insideQuantifier)
    {
        foreach (RegexNode item in items)
        {
            if (ContainsNestedQuantifier(item, insideQuantifier))
            {
                return true;
            }
        }

        return false;
    }
}