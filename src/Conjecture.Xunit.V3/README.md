# Conjecture.Xunit.V3

xUnit v3 adapter for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Discovers `[Property]` methods, generates random inputs from type-inferred strategies, and shrinks failing inputs to the smallest counterexample.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Xunit.V3
```

## Usage

```csharp
using Conjecture.Xunit.V3;

public class SortTests
{
    [Property]
    public bool Sorting_is_idempotent(List<int> items)
    {
        List<int> sorted = items.OrderBy(x => x).ToList();
        List<int> sortedTwice = sorted.OrderBy(x => x).ToList();
        return sorted.SequenceEqual(sortedTwice);
    }
}
```

Run with `dotnet test`. Conjecture executes the property 100 times against random `List<int>` inputs; on failure, it shrinks to the minimal failing list and reports a reproducible seed.

Set `[ConjectureSettings(ExportReproductionOnFailure = true)]` to write a runnable `.cs` repro alongside the failure. See [Export reproductions](https://ommundsen.dev/Conjecture/articles/how-to/export-repros.html).


## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
