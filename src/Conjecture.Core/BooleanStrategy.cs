using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class BooleanStrategy : Strategy<bool>
{
    internal override bool Generate(ConjectureData data) => data.NextBoolean();
}
