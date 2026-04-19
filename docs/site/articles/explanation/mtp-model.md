# Why MTP tests are executables

Microsoft Testing Platform (MTP) uses a different execution model from xUnit, NUnit, and MSTest. Understanding the difference explains why `Conjecture.TestingPlatform` projects require `OutputType=Exe` and why there is no runner to install.

## The traditional model

With xUnit, NUnit, or MSTest, the test project compiles to a class library (`.dll`). A separate runner — `dotnet test`, the vstest host, or an IDE adapter — loads that DLL as a plugin and calls into it via a well-known interface. The runner owns the process; the test framework is a guest.

This model requires:
- A test host binary that knows how to load framework DLLs
- Adapter packages that wire the framework to the vstest protocol
- `.runsettings` files or environment variables to pass configuration through the host

## The MTP model

MTP inverts the relationship. The test project compiles to an executable (`.exe`). The framework — `Conjecture.TestingPlatform` — is a library statically linked into that executable. When you run `dotnet test` or execute the binary directly, your code is the host.

The package supplies an entry point via MSBuild props, so you do not write `Program.cs`. Your test classes and `[Property]` methods are discovered by the framework at startup.

Consequences:
- No adapter DLLs, no `.runsettings`, no runner installation
- `dotnet test` and `dotnet run` both work — the binary is self-contained
- The executable can be invoked directly in CI without the .NET SDK test infrastructure

## Alignment with .NET's direction

MTP's executable model fits naturally with native AOT and single-file trimmed binaries, where reflection-based plugin loading is problematic. A statically linked binary can be inspected by the trimmer at publish time.

`Conjecture.TestingPlatform` uses reflection for test discovery (`[RequiresUnreferencedCode]`, `[RequiresDynamicCode]`), so it does not currently support native AOT publishing. Trim warnings are suppressed in the package. If AOT support is required, stay with an xUnit or NUnit adapter and a source-generated test runner.

## Further reading

- [Microsoft Testing Platform introduction](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
- [How to use the MTP adapter](../how-to/use-mtp-adapter.md)
