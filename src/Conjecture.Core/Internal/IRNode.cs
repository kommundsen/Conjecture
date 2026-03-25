namespace Conjecture.Core.Internal;

internal readonly struct IRNode
{
    internal IRNodeKind Kind { get; }
    internal ulong Value { get; }
    internal ulong Min { get; }
    internal ulong Max { get; }

    private IRNode(IRNodeKind kind, ulong value, ulong min, ulong max)
    {
        Kind = kind;
        Value = value;
        Min = min;
        Max = max;
    }

    internal static IRNode ForInteger(ulong value, ulong min, ulong max) =>
        new(IRNodeKind.Integer, value, min, max);

    internal static IRNode ForBoolean(bool value) =>
        new(IRNodeKind.Boolean, value ? 1UL : 0UL, 0UL, 1UL);

    internal static IRNode ForBytes(int length) =>
        new(IRNodeKind.Bytes, (ulong)length, 0UL, 0UL);
}
