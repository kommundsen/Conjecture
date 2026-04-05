# Conjecture.MSTest

MSTest adapter for [Conjecture.NET](https://github.com/kommundsen/Conjecture) property-based testing.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.MSTest
```

## Usage

```csharp
using Conjecture;

[TestClass]
public class MyTests
{
    [Property]
    public bool ReversingTwiceIsIdentity(List<int> xs)
    {
        var reversed = xs.AsEnumerable().Reverse().Reverse().ToList();
        return xs.SequenceEqual(reversed);
    }
}
```

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
