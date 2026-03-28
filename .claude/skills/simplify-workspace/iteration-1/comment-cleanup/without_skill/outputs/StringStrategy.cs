using System.Linq;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class StringStrategy : Strategy<string>
{
    private readonly char[] alphabet;
    private readonly int minLength;
    private readonly int maxLength;
    private readonly ulong lastIndex;

    internal StringStrategy(char[]? alphabet, int minLength, int maxLength)
    {
        this.alphabet = alphabet ?? Enumerable.Range(32, 95).Select(i => (char)i).ToArray();
        this.minLength = minLength;
        this.maxLength = maxLength;
        lastIndex = (ulong)(this.alphabet.Length - 1);
    }

    internal override string Next(ConjectureData data)
    {
        var length = (int)data.DrawInteger((ulong)minLength, (ulong)maxLength);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = alphabet[(int)data.DrawInteger(0, lastIndex)];
        }
        return new string(chars);
    }
}
