// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.Http;

namespace Conjecture.Http.Tests;

public class GenerateHttpTests
{
    [Fact]
    public void Http_EntryPoint_ReturnsBuilder()
    {
        HttpStrategyBuilder builder = Generate.Http("api");
        Assert.NotNull(builder);
    }

    [Fact]
    public void Build_WithGet_ProducesGetInteraction()
    {
        Strategy<HttpInteraction> strategy = Generate.Http("api").Get("/ping").Build();

        HttpInteraction sample = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("GET", sample.Method);
        Assert.Equal("/ping", sample.Path);
        Assert.Equal("api", sample.ResourceName);
        Assert.Null(sample.Body);
    }

    [Fact]
    public void Build_WithPost_IncludesMethodAndPath()
    {
        Strategy<HttpInteraction> strategy = Generate.Http("api").Post("/items").Build();

        HttpInteraction sample = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("POST", sample.Method);
        Assert.Equal("/items", sample.Path);
    }

    [Fact]
    public void Build_WithPostAndBody_CapturesBody()
    {
        object body = new { Name = "alice" };
        Strategy<HttpInteraction> strategy = Generate.Http("api").Post("/items", body).Build();

        HttpInteraction sample = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("POST", sample.Method);
        Assert.Same(body, sample.Body);
    }

    [Fact]
    public void Build_WithPut_IncludesMethodAndPath()
    {
        Strategy<HttpInteraction> strategy = Generate.Http("api").Put("/items/1").Build();

        HttpInteraction sample = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("PUT", sample.Method);
        Assert.Equal("/items/1", sample.Path);
    }

    [Fact]
    public void Build_WithDelete_IncludesMethod()
    {
        Strategy<HttpInteraction> strategy = Generate.Http("api").Delete("/items/1").Build();

        HttpInteraction sample = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("DELETE", sample.Method);
        Assert.Equal("/items/1", sample.Path);
        Assert.Null(sample.Body);
    }

    [Fact]
    public void Build_WithPatch_IncludesMethod()
    {
        Strategy<HttpInteraction> strategy = Generate.Http("api").Patch("/items/1").Build();

        HttpInteraction sample = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("PATCH", sample.Method);
    }

    [Fact]
    public void Build_WithHeaders_AttachesHeaders()
    {
        Dictionary<string, string> headers = new() { ["X-Trace"] = "abc" };
        Strategy<HttpInteraction> strategy = Generate.Http("api")
            .Get("/ping")
            .WithHeaders(headers)
            .Build();

        HttpInteraction sample = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.NotNull(sample.Headers);
        Assert.Equal("abc", sample.Headers!["X-Trace"]);
    }

    [Fact]
    public void Build_WithResource_OverridesResourceName()
    {
        Strategy<HttpInteraction> strategy = Generate.Http("api")
            .Get("/ping")
            .WithResource("orders")
            .Build();

        HttpInteraction sample = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("orders", sample.ResourceName);
    }

    [Fact]
    public void Build_WithBodyStrategy_OverridesLiteralBody()
    {
        object literalBody = "literal";
        Strategy<object?> bodyStrategy = Generate.Just<object?>("from-strategy");

        Strategy<HttpInteraction> strategy = Generate.Http("api")
            .Post("/items", literalBody)
            .WithBodyStrategy(bodyStrategy)
            .Build();

        HttpInteraction sample = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("from-strategy", sample.Body);
    }

    [Fact]
    public void Build_WithBodyStrategy_OnPutWithoutLiteralBody_UsesStrategyBody()
    {
        Strategy<object?> bodyStrategy = Generate.Just<object?>("put-body");
        Strategy<HttpInteraction> strategy = Generate.Http("api")
            .Put("/items/1")
            .WithBodyStrategy(bodyStrategy)
            .Build();

        HttpInteraction sample = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal("put-body", sample.Body);
    }

    [Fact]
    public void Build_ReturnsDeterministicResults_ForSameSeed()
    {
        Strategy<object?> bodyStrategy = Generate.Integers(0, 1_000_000).Select(i => (object?)i);
        Strategy<HttpInteraction> strategy = Generate.Http("api")
            .Post("/items")
            .WithBodyStrategy(bodyStrategy)
            .Build();

        IReadOnlyList<HttpInteraction> a = DataGen.Sample(strategy, count: 5, seed: 42UL);
        IReadOnlyList<HttpInteraction> b = DataGen.Sample(strategy, count: 5, seed: 42UL);

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Body, b[i].Body);
        }
    }

    [Fact]
    public void Build_IsComposable_WithOtherStrategies()
    {
        Strategy<HttpInteraction> httpStrategy = Generate.Http("api").Get("/ping").Build();
        Strategy<(HttpInteraction, int)> paired = httpStrategy.Zip(Generate.Integers(0, 10));

        (HttpInteraction interaction, int number) = DataGen.SampleOne(paired, seed: 1UL);

        Assert.Equal("GET", interaction.Method);
        Assert.InRange(number, 0, 10);
    }

    [Fact]
    public void Http_NullResourceName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Generate.Http(null!));
    }

    [Fact]
    public void Get_NullPath_Throws()
    {
        HttpStrategyBuilder builder = Generate.Http("api");
        Assert.Throws<ArgumentNullException>(() => builder.Get(null!));
    }

    [Fact]
    public void WithBodyStrategy_Null_Throws()
    {
        HttpStrategyBuilder builder = Generate.Http("api").Post("/items");
        Assert.Throws<ArgumentNullException>(() => builder.WithBodyStrategy(null!));
    }
}
