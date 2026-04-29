# Conjecture.Time

Time-focused strategies for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Provides boundary-aware `DateTimeOffset` extensions and `FakeTimeProvider` generation for deterministic time-dependent tests.

## Install

```
dotnet add package Conjecture.Time
```

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Time;

// Generate values near DST transitions
[Property]
public bool DstSafe([From<DateTimeOffsetArbitrary>] DateTimeOffset dt)
{
    DateTimeOffset nearDst = Strategy.DateTimeOffsets().NearDstTransition();
    // ...
}

// Generate a cluster of clocks with bounded skew
[Property]
public bool ClockSkewTolerant(int x)
{
    FakeTimeProvider[] clocks = DataGen.SampleOne(
        Strategy.ClockSet(nodeCount: 3, maxSkew: TimeSpan.FromSeconds(5)));
    // ...
}
```

## API

| Method | Returns | Description |
|---|---|---|
| `Strategy.TimeZones()` | `Strategy<TimeZoneInfo>` | System time zones, shrinks toward UTC |
| `Strategy.ClockSet(nodeCount, maxSkew)` | `Strategy<FakeTimeProvider[]>` | Array of clocks with bounded skew |
| `.NearMidnight()` | `Strategy<DateTimeOffset>` | Values within ~30 min of midnight |
| `.NearLeapYear()` | `Strategy<DateTimeOffset>` | Values within ~1 day of Feb 29 |
| `.NearEpoch()` | `Strategy<DateTimeOffset>` | Values near Unix epoch, Y2K38, min/max |
| `.NearDstTransition(zone?)` | `Strategy<DateTimeOffset>` | Values within ~1 hour of a DST transition |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)