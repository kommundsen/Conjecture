// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests;

public class ArbitraryAutoDiscoveryTests
{
    // ─── Domain type ─────────────────────────────────────────────────────────

    public record Person(string Name, int Age);

    // ─── Auto-discovered provider (carries [Arbitrary]) ──────────────────────

    [Arbitrary]
    public sealed class PersonArbitrary : IStrategyProvider<Person>
    {
        public Strategy<Person> Create() =>
            Strategy.Strings().Zip(Strategy.Integers<int>(0, 120), (name, age) => new Person(name, age));
    }

    // ─── Sentinel provider (no [Arbitrary]) used in precedence test ───────────

    public sealed class SentinelPersonArbitrary : IStrategyProvider<Person>
    {
        public Strategy<Person> Create() =>
            Strategy.Integers<int>().Select(_ => new Person("SENTINEL", -999));
    }

    // ─── Helper stubs ─────────────────────────────────────────────────────────

#pragma warning disable IDE0060
    private static void PersonParam(Person p) { }
    private static void FromSentinelPerson([From<SentinelPersonArbitrary>] Person p) { }
    private static void IntParam(int n) { }
#pragma warning restore IDE0060

    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    private static ParameterInfo[] ParamsOf(string methodName) =>
        typeof(ArbitraryAutoDiscoveryTests)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

    // ─── Auto-discovery ───────────────────────────────────────────────────────

    [Fact]
    public void Resolve_PersonParam_WithArbitraryProvider_ReturnsPerson()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(PersonParam)), MakeData());
        Assert.IsType<Person>(args[0]);
    }

    [Fact]
    public void Resolve_PersonParam_AutoDiscoveredValues_AreWithinProviderRange()
    {
        for (int i = 0; i < 30; i++)
        {
            object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(PersonParam)), MakeData((ulong)i));
            Person person = Assert.IsType<Person>(args[0]);
            Assert.NotNull(person.Name);
            Assert.InRange(person.Age, 0, 120);
        }
    }

    // ─── Explicit [From<T>] takes precedence ─────────────────────────────────

    [Fact]
    public void Resolve_ExplicitFromAttribute_TakesPrecedenceOverAutoDiscovery()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(FromSentinelPerson)), MakeData());
        Person person = Assert.IsType<Person>(args[0]);
        Assert.Equal("SENTINEL", person.Name);
        Assert.Equal(-999, person.Age);
    }

    // ─── Unrelated types still fall through to type-switch ───────────────────

    [Fact]
    public void Resolve_IntParam_NoArbitraryProvider_FallsThroughToTypeInference()
    {
        object[] args = SharedParameterStrategyResolver.Resolve(ParamsOf(nameof(IntParam)), MakeData());
        Assert.IsType<int>(args[0]);
    }

    // ─── Shrinking integration ────────────────────────────────────────────────

    [Fact]
    public void Resolve_PersonParam_AutoDiscovery_IsDeterministicAcrossReplays()
    {
        ParameterInfo[] parameters = ParamsOf(nameof(PersonParam));
        object[] first = SharedParameterStrategyResolver.Resolve(parameters, MakeData(7UL));
        object[] second = SharedParameterStrategyResolver.Resolve(parameters, MakeData(7UL));

        Person p1 = Assert.IsType<Person>(first[0]);
        Person p2 = Assert.IsType<Person>(second[0]);
        Assert.Equal(p1, p2);
    }
}