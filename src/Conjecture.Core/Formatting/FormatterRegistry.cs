namespace Conjecture.Core.Formatting;

/// <summary>Global registry mapping types to their <see cref="IStrategyFormatter{T}"/> instances.</summary>
public static class FormatterRegistry
{
    /// <summary>Registers <paramref name="formatter"/> for type <typeparamref name="T"/>, replacing any existing registration.</summary>
    public static void Register<T>(IStrategyFormatter<T>? formatter) =>
        Holder<T>.Instance = formatter;

    /// <summary>Returns the registered formatter for <typeparamref name="T"/>, or <see langword="null"/> if none is registered.</summary>
    public static IStrategyFormatter<T>? Get<T>() =>
        Holder<T>.Instance;

    private static class Holder<T>
    {
        public static volatile IStrategyFormatter<T>? Instance;
    }
}
