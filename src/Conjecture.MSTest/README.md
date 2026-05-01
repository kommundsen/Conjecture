# Conjecture.MSTest

MSTest adapter for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Use `[Property]` alongside `[TestClass]` to drive an MSTest method with random inputs and automatic shrinking on failure.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.MSTest
```

## Usage

```csharp
using Conjecture.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
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
