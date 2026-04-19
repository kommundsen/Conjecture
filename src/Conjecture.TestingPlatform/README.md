# Conjecture.TestingPlatform

Microsoft Testing Platform adapter for [Conjecture.NET](https://github.com/kommundsen/Conjecture) property-based testing.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.TestingPlatform
```

## Usage

Add the package reference to your test project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Conjecture.Core" Version="*" />
    <PackageReference Include="Conjecture.TestingPlatform" Version="*" />
  </ItemGroup>
</Project>
```

Then write property-based tests:

```csharp
using Conjecture;

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
- [Docs](https://github.com/kommundsen/Conjecture/blob/main/docs/site)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
