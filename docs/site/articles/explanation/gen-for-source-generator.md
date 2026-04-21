# Understanding Gen.For&lt;T&gt;() source generation

`Generate.For<T>()` is the call-site entry point for a strategy the compiler derived from your type at build time. This page explains why source generation was chosen over alternatives, how the emitted code is wired together, and how `Generate.For<T>()` fits into a broader property test.

## Why not reflection?

The obvious alternative to source generation is runtime reflection: inspect the type's constructor at runtime, resolve a strategy for each parameter type, and assemble a `Generate.Compose` call dynamically.

Reflection works — but it has three costs that source generation avoids:

**AOT and trim compatibility.** .NET 8+ AOT compilation and IL trimming eliminate types and members that are not statically referenced. A reflection-based generator would force you to annotate every DTO with `[DynamicallyAccessedMembers]` or disable trimming for the assembly. Source generation produces ordinary C# code that the trimmer understands natively.

**Compile-time feedback.** When reflection fails — because the constructor is private, a parameter type has no strategy, or the type is abstract — the error surfaces at runtime, often in a test run. Source generation surfaces the same error as a Roslyn diagnostic at build time, before a test ever runs. CON312 ("no registered provider") is impossible to reach in a green build; CON202 ("no resolvable strategy for parameter") is a warning before you write a single test.

**Debuggability.** The generated code is ordinary C# you can step through. There are no `MethodInfo.Invoke` frames, no dynamic proxy layers, no `Expression.Lambda.Compile` pipelines.

## How the generator works

When the Roslyn incremental generator sees `[Arbitrary]` on a `partial` type, it performs three steps at build time:

**1. Type model extraction.** The generator reads the type's constructor — primary constructor for records, most-accessible constructor for classes and structs. It maps each parameter to a strategy expression using the primitive mapping table, respecting `[GenRange]`, `[GenStringLength]`, `[GenRegex]`, and `[GenMaxDepth]` attributes.

**2. Code emission.** The generator emits two things alongside the type:
- An `IStrategyProvider<T>` implementation (e.g. `OrderArbitrary`) that calls `Generate.Compose` with the resolved strategies.
- An override-aware variant (`CreateWithOverrides`) that accepts a `ForConfiguration<T>` and substitutes overridden properties.

A typical emission for `Order(Guid Id, string Customer, decimal Total)` looks like:

```csharp
// Auto-generated
internal sealed class OrderArbitrary : IStrategyProvider<Order>
{
    public Strategy<Order> Create() =>
        Generate.Compose<Order>(ctx => new Order(
            ctx.Generate(Generate.Guids()),
            ctx.Generate(Generate.Strings()),
            ctx.Generate(Generate.Decimals())));

    public static Strategy<Order> CreateWithOverrides(ForConfiguration<Order> cfg) =>
        Generate.Compose<Order>(ctx => new Order(
            ctx.Generate(cfg.TryGet<Guid>("Id") ?? Generate.Guids()),
            ctx.Generate(cfg.TryGet<string>("Customer") ?? Generate.Strings()),
            ctx.Generate(cfg.TryGet<decimal>("Total") ?? Generate.Decimals())));
}
```

**3. Registry wiring.** The generator also emits a `[ModuleInitializer]` that registers both the provider and the override factory with `GenForRegistry`:

```csharp
[ModuleInitializer]
internal static void RegisterOrderArbitrary()
{
    GenForRegistry.Register(typeof(Order), static () => new OrderArbitrary());
    GenForRegistry.RegisterOverride(typeof(Order),
        static cfg => OrderArbitrary.CreateWithOverrides((ForConfiguration<Order>)cfg));
}
```

`[ModuleInitializer]` runs before any user code in the assembly — registration is guaranteed to be complete before the first `Generate.For<Order>()` call.

## How `Generate.For<T>()` resolves a strategy

`Generate.For<T>()` delegates to `GenForRegistry.Resolve<T>()`, which looks up the registered factory by `typeof(T)` in a `ConcurrentDictionary`. The lookup is O(1) and allocation-free after the first call.

`Generate.For<T>(cfg => ...)` takes the override path: it constructs a `ForConfiguration<T>`, runs your callback, then calls `GenForRegistry.ResolveWithOverrides<T>(cfg)`, which invokes the override-aware factory. The override factory calls `cfg.TryGet<TProp>(propertyName)` for each parameter — if an override exists it uses it, otherwise it falls back to the default strategy.

The result is a `Strategy<T>` like any other. It composes, shrinks, and replays exactly as if you had written the `Generate.Compose` call by hand.

## Composing with property tests

`Generate.For<T>()` returns a `Strategy<T>`. You can use it anywhere a strategy is accepted:

```csharp
// As a standalone strategy
Strategy<Order> orders = Generate.For<Order>();

// Composed with other strategies
Strategy<(Order, Payment)> pairs = Generate.Tuples(
    Generate.For<Order>(),
    Generate.For<Payment>());

// As a stateful testing source
Strategy<StateMachineRun<ShopState>> runs =
    Generate.StateMachine<ShopMachine, ShopState, ShopCommand>();
```

For `[Property]` parameters, `[From<OrderArbitrary>]` is the idiomatic shorthand — it passes `new OrderArbitrary().Create()` as the parameter strategy. `Generate.For<T>()` gives you the same strategy as an expression when you need to compose it or pass it around.

> [!NOTE]
> CON105 fires when a `[Property]` parameter's type has an `[Arbitrary]` provider but `[From<T>]` is absent. The analyzer nudges you toward the declarative style, but there is no behavioral difference between `[From<OrderArbitrary>]` and building the strategy manually with `Generate.For<Order>()`.

## The registry and AOT

`GenForRegistry` is a `public static` class backed by two `ConcurrentDictionary<Type, Func<...>>` fields. Keeping it public allows generated code in user assemblies to call `Register` and `RegisterOverride`. The dictionaries use `Type` as the key rather than a generic type parameter, which is AOT-safe: no `MakeGenericType`, no `MethodInfo.Invoke`, no `Activator.CreateInstance`.

The generated factories are `static` lambdas (`static () => new OrderArbitrary()`). `static` lambdas are compiler-lowered to static methods, so they produce no closures and hold no references that would confuse the trimmer.

## Further reading

- [How to use Gen.For&lt;T&gt;()](../how-to/use-gen-for.md) — step-by-step recipes
- [Reference: Gen.For&lt;T&gt;()](../reference/gen-for.md) — attribute table, primitive mapping, diagnostics
- [How to use source generators](../how-to/use-source-generators.md) — `[Arbitrary]` basics
- [Understanding sealed hierarchy strategies](sealed-hierarchy-strategies.md) — hierarchy mode for abstract types
