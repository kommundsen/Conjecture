# Network strategies reference

Strategies for stdlib network and address-format primitives. All factories are in `Conjecture.Core` and need no extra package.

---

## IP address strategies

### `IPAddressKind` enum

```csharp
[Flags]
public enum IPAddressKind { V4 = 1, V6 = 2, Both = V4 | V6 }
```

Selects the address family or families produced by `Strategy.IPAddresses`.

### `Strategy.IPAddresses(IPAddressKind kind = IPAddressKind.Both)`

```csharp
Strategy<IPAddress> Strategy.IPAddresses(IPAddressKind kind = IPAddressKind.Both)
```

Generates `IPAddress` values uniformly across the byte space of the requested family. V4 addresses are drawn from 4 random bytes; V6 from 16. With `IPAddressKind.Both`, each example flips a coin to pick the family. Shrinks toward the all-zero address (`0.0.0.0` / `::`).

```csharp
Strategy<IPAddress> any = Strategy.IPAddresses();                       // V4 or V6
Strategy<IPAddress> v4 = Strategy.IPAddresses(IPAddressKind.V4);        // V4 only
Strategy<IPAddress> v6 = Strategy.IPAddresses(IPAddressKind.V6);        // V6 only
```

### `Strategy.IPEndPoints(Strategy<IPAddress>? addresses = null, Strategy<int>? ports = null)`

```csharp
Strategy<IPEndPoint> Strategy.IPEndPoints(
    Strategy<IPAddress>? addresses = null,
    Strategy<int>? ports = null)
```

Composes an `IPAddress` strategy and a port strategy into `IPEndPoint`. When `addresses` is null, uses `Strategy.IPAddresses(IPAddressKind.Both)`; when `ports` is null, uses `Strategy.Integers<int>(0, 65535)`. A custom port strategy producing a value outside `[0, 65535]` triggers `ArgumentOutOfRangeException` on `Generate`.

```csharp
Strategy<IPEndPoint> defaults = Strategy.IPEndPoints();
Strategy<IPEndPoint> highPorts = Strategy.IPEndPoints(
    ports: Strategy.Integers<int>(49152, 65535));
```

---

## Host name strategy

### `Strategy.Hosts(int minLabels = 1, int maxLabels = 3)`

```csharp
Strategy<string> Strategy.Hosts(int minLabels = 1, int maxLabels = 3)
```

Generates DNS-like host names: `minLabels..maxLabels` labels of lowercase alphanumerics joined by `.`, with a TLD-shaped final label (lowercase letters only, length 2–6). Shrinks toward the shortest valid host (e.g., `aa` for `minLabels = 1`, `a.aa` for `minLabels = 2`).

```csharp
Strategy<string> hosts = Strategy.Hosts();           // 1..3 labels
Strategy<string> fqdns = Strategy.Hosts(2, 4);       // 2..4 labels — always has a dot
```

`minLabels` must be `>= 1` and `maxLabels` must be `>= minLabels`; both checks throw `ArgumentOutOfRangeException`.

---

## URI strategy

### `Strategy.Uris(UriKind kind = UriKind.Absolute, IReadOnlyList<string>? schemes = null)`

```csharp
Strategy<Uri> Strategy.Uris(
    UriKind kind = UriKind.Absolute,
    IReadOnlyList<string>? schemes = null)
```

Generates `Uri` values from a controlled grammar: scheme, host (DNS or IP literal), optional port, and 0–3 path segments. When `schemes` is null, uses `["http", "https"]`. When `kind` is `UriKind.Absolute`, every example is absolute; `UriKind.Relative` emits the path portion only; `UriKind.RelativeOrAbsolute` mixes the two. IPv6 host literals are emitted with surrounding `[` `]` brackets.

```csharp
Strategy<Uri> webUris = Strategy.Uris();                              // absolute http/https
Strategy<Uri> wsUris = Strategy.Uris(schemes: ["ws", "wss"]);
Strategy<Uri> relative = Strategy.Uris(UriKind.Relative);
Strategy<Uri> mixed = Strategy.Uris(UriKind.RelativeOrAbsolute);
```

`schemes` must be non-empty; an empty list throws `ArgumentException`.

---

## Email address strategies

### `Strategy.EmailAddresses()`

```csharp
Strategy<MailAddress> Strategy.EmailAddresses()
```

Generates `MailAddress` values with locally-generated user and host parts. The local part is an `Identifiers()`-shaped token; the host comes from `Strategy.Hosts()` (so every address has a TLD-shaped final label). Aimed at inputs that `MailAddress` accepts — does not cover the full RFC 5322 grammar (no quoted local-parts, comments, or IDN).

### `Strategy.EmailAddressStrings()`

```csharp
Strategy<string> Strategy.EmailAddressStrings()
```

Like `EmailAddresses()` but returns the raw `<local>@<host>` string. Round-trips cleanly through `new MailAddress(s).Address`.

```csharp
Strategy<MailAddress> typed = Strategy.EmailAddresses();
Strategy<string> strings = Strategy.EmailAddressStrings();
```

---

## Strategy.For&lt;T&gt; resolution

Each network primitive registers a default provider, so `Strategy.For<T>()` returns a working strategy without further wiring:

| Type | Resolves to |
| --- | --- |
| `IPAddress` | `Strategy.IPAddresses()` |
| `IPEndPoint` | `Strategy.IPEndPoints()` |
| `Uri` | `Strategy.Uris()` |
| `MailAddress` | `Strategy.EmailAddresses()` |

See [Strategy.For&lt;T&gt;()](generate-for.md) for the resolver contract and how to override defaults per test.

## See also

- [How to property-test network code](../how-to/property-test-network-code.md) — worked example with shrinking
- [Strategies reference](strategies.md) — full strategy index
