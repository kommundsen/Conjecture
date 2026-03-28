using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class StringStrategy : Strategy<string>
{
    private static readonly char[] DefaultAlphabet = BuildDefaultAlphabet();
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
        if (length == 0)
        {
            return string.Empty;
        }
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            var index = (int)data.DrawInteger(0, (ulong)(alphabet.Length - 1));
            chars[i] = alphabet[index];
        }
        return new string(chars);
    }

    private static char[] BuildDefaultAlphabet()
    {
        // printable ASCII: 0x20 to 0x7E
        var chars = new char[0x7F - 0x20];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)(0x20 + i);
        }
        return chars;
    }
}
