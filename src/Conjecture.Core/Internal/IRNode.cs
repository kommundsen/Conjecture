namespace Conjecture.Core.Internal;

internal readonly struct IRNode
{
    internal IRNodeKind Kind { get; }
    internal ulong Value { get; }
    internal ulong Min { get; }
    internal ulong Max { get; }
    internal byte[]? RawBytes { get; }

    private IRNode(IRNodeKind kind, ulong value, ulong min, ulong max, byte[]? rawBytes = null)
    {
        Kind = kind;
        Value = value;
        Min = min;
        Max = max;
        RawBytes = rawBytes;
    }

    internal static IRNode ForInteger(ulong value, ulong min, ulong max) =>
        new(IRNodeKind.Integer, value, min, max);

    internal static IRNode ForBoolean(bool value) =>
        new(IRNodeKind.Boolean, value ? 1UL : 0UL, 0UL, 1UL);

    internal static IRNode ForBytes(int length, byte[]? data = null) =>
        new(IRNodeKind.Bytes, (ulong)length, 0UL, 0UL, data);

    internal static IRNode ForFloat64(ulong value, ulong min, ulong max) =>
        new(IRNodeKind.Float64, value, min, max);

    internal static IRNode ForFloat32(ulong value, ulong min, ulong max) =>
        new(IRNodeKind.Float32, value, min, max);

    internal static IRNode ForStringLength(ulong value, ulong min, ulong max) =>
        new(IRNodeKind.StringLength, value, min, max);

    internal static IRNode ForStringChar(ulong value, ulong min, ulong max) =>
        new(IRNodeKind.StringChar, value, min, max);

    internal bool IsIntegerLike =>
        Kind is IRNodeKind.Integer or IRNodeKind.Float64 or IRNodeKind.Float32
             or IRNodeKind.StringLength or IRNodeKind.StringChar;

    internal IRNode WithValue(ulong newValue) => Kind switch
    {
        IRNodeKind.Float64 => ForFloat64(newValue, Min, Max),
        IRNodeKind.Float32 => ForFloat32(newValue, Min, Max),
        IRNodeKind.StringLength => ForStringLength(newValue, Min, Max),
        IRNodeKind.StringChar => ForStringChar(newValue, Min, Max),
        _ => ForInteger(newValue, Min, Max),
    };
}
