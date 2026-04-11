# How to reproduce a failure

Every Conjecture failure message includes the seed used to generate the counterexample. Pin it to replay the exact same inputs on every run.

## Find the seed in the failure output

```text
Falsifying example after 42 examples (seed: 0xDEADBEEF12345678):
  value = 42
```

## Pin the seed with `[Property(Seed = ...)]`

# [xUnit v2](#tab/xunit-v2)

```csharp
[Property(Seed = 0xDEADBEEF12345678)]
public bool My_property(int value) => ...;
```

# [xUnit v3](#tab/xunit-v3)

```csharp
[Property(Seed = 0xDEADBEEF12345678)]
public bool My_property(int value) => ...;
```

# [NUnit](#tab/nunit)

```csharp
[Property(Seed = 0xDEADBEEF12345678)]
public bool My_property(int value) => ...;
```

# [MSTest](#tab/mstest)

```csharp
[Property(Seed = 0xDEADBEEF12345678)]
public bool My_property(int value) => ...;
```

***

The test now runs the same inputs deterministically every time. Debugging, adding logging, and stepping through the property all work with the exact failing case.

## Remove the seed once the bug is fixed

After fixing the bug, remove the `Seed` attribute:

```csharp
// Remove this line once the bug is fixed:
// [Property(Seed = 0xDEADBEEF12345678)]
[Property]
public bool My_property(int value) => ...;
```

The property resumes random exploration and will find new counterexamples if the fix is incomplete.

## Pin via `[ConjectureSettings]` (assembly-level)

To fix the seed for all tests during a debugging session:

```csharp
[assembly: ConjectureSettings(Seed = 0xDEADBEEF12345678)]
```

> [!WARNING]
> Assembly-level seed pinning makes all tests in the assembly deterministic. Remove it before committing.

## See also

- [How to export repros to files](export-repros.md) — save shrunk counterexamples as artifacts
- [Reference: Settings](../reference/settings.md) — `Seed` and other `[Property]` settings
