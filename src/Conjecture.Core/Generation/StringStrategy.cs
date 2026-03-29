using Conjecture.Core.Internal;

namespace Conjecture.Core.Generation;

internal sealed class StringStrategy : Strategy<string>
{
    private readonly ulong minLength;
    private readonly ulong maxLength;
    private readonly ulong minCodepoint;
    private readonly ulong maxCodepoint;
    private readonly string? alphabet;

    internal StringStrategy(int minLength = 0, int maxLength = 20, int minCodepoint = 32, int maxCodepoint = 126)
    {
        this.minLength = (ulong)minLength;
        this.maxLength = (ulong)maxLength;
        this.minCodepoint = (ulong)minCodepoint;
        this.maxCodepoint = (ulong)maxCodepoint;
    }

    internal StringStrategy(string alphabet, int minLength = 0, int maxLength = 20)
    {
        if (alphabet.Length == 0)
        {
            throw new ArgumentException("Alphabet must not be empty.", nameof(alphabet));
        }
        this.alphabet = alphabet;
        this.minLength = (ulong)minLength;
        this.maxLength = (ulong)maxLength;
        this.minCodepoint = 0;
        this.maxCodepoint = 0;
    }

    internal override string Next(ConjectureData data)
    {
        var length = (int)data.DrawStringLength(minLength, maxLength);
        var chars = new char[length];
        if (alphabet is not null)
        {
            for (var i = 0; i < length; i++)
            {
                chars[i] = alphabet[(int)data.DrawStringChar(0, (ulong)(alphabet.Length - 1))];
            }
        }
        else
        {
            for (var i = 0; i < length; i++)
            {
                chars[i] = (char)data.DrawStringChar(minCodepoint, maxCodepoint);
            }
        }
        return new string(chars);
    }
}
