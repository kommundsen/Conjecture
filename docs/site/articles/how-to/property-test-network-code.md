# How to property-test network code

Use `Strategy.IPAddresses`, `Strategy.IPEndPoints`, `Strategy.Uris`, and `Strategy.EmailAddresses` to drive parsers, validators, and routing code with hundreds of address-shaped inputs per run — and let the shrinker hand you the smallest counterexample.

## Property-test a URL parser

Suppose you have a custom parser that pulls the host out of an absolute URL:

```csharp
public static class UrlInspector
{
    public static string GetHost(string url) =>
        new Uri(url, UriKind.Absolute).Host;
}
```

Write a property that proves the parser agrees with `Uri.Host` for any well-formed URL Conjecture can produce:

```csharp
using Conjecture.Core;
using Xunit;

public class UrlInspectorTests
{
    [Property]
    public void GetHost_AgreesWithUri_ForGeneratedUrls(Uri url)
    {
        // Strategy.For<Uri>() resolves to Strategy.Uris() — http/https absolute by default
        Assert.Equal(url.Host, UrlInspector.GetHost(url.OriginalString));
    }
}
```

`[Property]` runs the test ~100 times by default, each invocation receiving a fresh `Uri`. To restrict the URL shape, build the strategy explicitly:

```csharp
[Property]
public void GetHost_AgreesWithUri_ForWebSocketUrls()
{
    Strategy<Uri> wsUrls = Strategy.Uris(schemes: ["ws", "wss"]);
    DataGen.ForAll(wsUrls, url =>
    {
        Assert.Equal(url.Host, UrlInspector.GetHost(url.OriginalString));
    });
}
```

## Property-test an IP filter

A property test makes short work of "the implementation handles every IPv4-mapped IPv6 address" or "loopback always passes":

```csharp
public static class IpFilter
{
    public static bool IsAllowed(IPAddress addr) =>
        addr.AddressFamily switch
        {
            AddressFamily.InterNetwork => !IPAddress.IsLoopback(addr),
            AddressFamily.InterNetworkV6 => addr.IsIPv6LinkLocal == false,
            _ => false,
        };
}

[Property]
public void IpFilter_NeverThrows(IPAddress addr)
{
    // any IPv4 or IPv6 address — no exception expected
    _ = IpFilter.IsAllowed(addr);
}
```

If `IsAllowed` ever throws, Conjecture shrinks the failing address toward `0.0.0.0` (V4) or `::` (V6) — pinpointing whether the bug is family-specific or applies to the all-zero edge.

## Read a shrunk counterexample

Suppose `IpFilter.IsAllowed` had a subtle bug: it dereferences `addr` after a null check that always passes. The first failing run might report:

```
Property failed after 47 examples
Falsifying counterexample: 203.0.113.42
Shrunk to: 0.0.0.0
```

Conjecture starts with whatever address triggered the failure (`203.0.113.42` here) and uses integer-byte minimisation to drive each octet to zero, stopping at the smallest address that still fails. The `0.0.0.0` you see in the report is your repro — paste it into a unit test if you want a fast regression guard:

```csharp
[Fact]
public void IpFilter_AllowsAllZeroAddress() =>
    Assert.True(IpFilter.IsAllowed(IPAddress.Parse("0.0.0.0")));
```

## Property-test an email validator

`Strategy.EmailAddresses` produces strings that round-trip through `new MailAddress(s)`. That is sufficient for stress-testing validators that delegate to `MailAddress` themselves:

```csharp
[Property]
public void Validator_AcceptsAllAddressesMailAddressAccepts(MailAddress address)
{
    // If MailAddress could parse it, our validator should accept it.
    Assert.True(EmailValidator.IsValid(address.Address));
}
```

For the inverse direction (rejecting malformed input), pair this with a hand-written negative-case suite or a regex-based negative generator — `Strategy.EmailAddresses` is intentionally narrow and won't surface RFC 5322 quoted local-parts or comments.

## See also

- [Network strategies reference](../reference/network-strategies.md) — full API surface
- [How to reproduce a failure](reproduce-a-failure.md) — replaying counterexamples from logs
- [Strategy.For&lt;T&gt;()](../reference/generate-for.md) — overriding the default resolver
