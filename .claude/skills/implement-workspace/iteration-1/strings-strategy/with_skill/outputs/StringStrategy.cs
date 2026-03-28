using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class StringStrategy : Strategy<string>
{
    private static readonly char[] DefaultAlphabet =
        Enumerable.Range(32, 95).Select(i => (char)i).ToArray(); // printable ASCII

    private readonly char[] alphabet;
    private readonly int minLength;
    private readonly int maxLength;

    internal StringStrategy(char[]? alphabet, int minLength, int maxLength)
    {
        this.alphabet = alphabet ?? DefaultAlphabet;
        this.minLength = minLength;
        this.maxLength = maxLength;
    }

    internal override string Next(ConjectureData data)
    {
        var length = (int)data.DrawInteger((ulong)minLength, (ulong)maxLength);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = alphabet[data.DrawInteger(0, (ulong)(alphabet.Length - 1))];
        }
        return new string(chars);
    }
}
