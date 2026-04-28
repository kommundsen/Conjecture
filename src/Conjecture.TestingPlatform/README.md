# Conjecture.TestingPlatform

[Microsoft Testing Platform](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro) adapter for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Discovers `[Property]` methods and runs them under MTP without xUnit/NUnit/MSTest.

## Install

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Conjecture.Core" Version="*" />
    <PackageReference Include="Conjecture.TestingPlatform" Version="*" />
  </ItemGroup>
</Project>
```

## Usage

```csharp
using Conjecture.TestingPlatform;

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

Run the test executable directly (`dotnet run` or the produced exe). Conjecture executes the property 100 times against random `List<int>` inputs; on failure, it shrinks to the minimal failing list and reports a reproducible seed.

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
