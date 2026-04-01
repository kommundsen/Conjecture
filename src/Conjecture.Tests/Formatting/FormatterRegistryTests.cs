using Conjecture.Core;

namespace Conjecture.Tests.Formatting;

public class FormatterRegistryTests : IDisposable
{
    private readonly IStrategyFormatter<int>? originalInt = FormatterRegistry.Get<int>();
    private readonly IStrategyFormatter<double>? originalDouble = FormatterRegistry.Get<double>();
    private readonly IStrategyFormatter<string>? originalString = FormatterRegistry.Get<string>();

    public void Dispose()
    {
        FormatterRegistry.Register(originalInt);
        FormatterRegistry.Register(originalDouble);
        FormatterRegistry.Register(originalString);
    }

    [Fact]
    public void Get_AfterRegister_ReturnsRegisteredFormatter()
    {
        var formatter = new StubFormatter<int>();
        FormatterRegistry.Register<int>(formatter);

        var result = FormatterRegistry.Get<int>();

        Assert.Same(formatter, result);
    }

    [Fact]
    public void Get_UnregisteredType_ReturnsNull()
    {
        FormatterRegistry.Register<double>(null!);

        var result = FormatterRegistry.Get<double>();

        Assert.Null(result);
    }

    [Fact]
    public void Register_ReplacesExisting_NewFormatterIsReturned()
    {
        var first = new StubFormatter<string>();
        var second = new StubFormatter<string>();
        FormatterRegistry.Register<string>(first);
        FormatterRegistry.Register<string>(second);

        var result = FormatterRegistry.Get<string>();

        Assert.Same(second, result);
    }

    private sealed class StubFormatter<T> : IStrategyFormatter<T>
    {
        public string Format(T value) => value?.ToString() ?? "null";
    }
}
