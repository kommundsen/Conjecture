// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Tool.Tests.Plan.TestModels;

public class Location
{
    public required int CityCode { get; init; }
}

public class StringSource
{
    public required string Id { get; init; }
}

public sealed class LocationProvider : IStrategyProvider<Location>
{
    public Strategy<Location> Create() => Strategy.Integers<int>(1, 999)
        .Select(static code => new Location { CityCode = code });
}

public class NoCtorModel
{
    public required int Value { get; init; }
}

public sealed class NoCtorModelProvider(int seed) : IStrategyProvider<NoCtorModel>
{
    private readonly int seed = seed;

    public Strategy<NoCtorModel> Create() => Strategy.Integers<int>(1, 100)
        .Select(static v => new NoCtorModel { Value = v });
}