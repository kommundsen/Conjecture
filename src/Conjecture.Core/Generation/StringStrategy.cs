using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class StringStrategy : Strategy<string>
{
    private readonly ulong minLength;
    private readonly ulong maxLength;
    private readonly ulong minCodepoint;
    private readonly ulong maxCodepoint;

    internal StringStrategy(int minLength = 0, int maxLength = 100, int minCodepoint = 32, int maxCodepoint = 126)
    {
        this.minLength = (ulong)minLength;
        this.maxLength = (ulong)maxLength;
        this.minCodepoint = (ulong)minCodepoint;
        this.maxCodepoint = (ulong)maxCodepoint;
    }

    internal override string Next(ConjectureData data)
    {
        var length = (int)data.DrawInteger(minLength, maxLength);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = (char)data.DrawInteger(minCodepoint, maxCodepoint);
        }
        return new string(chars);
    }
}
