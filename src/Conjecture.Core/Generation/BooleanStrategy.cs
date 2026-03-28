using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class BooleanStrategy : Strategy<bool>
{
    internal override bool Next(ConjectureData data) => data.DrawBoolean();
}
