// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Conjecture.Core;
using Conjecture.Http;

using Microsoft.Extensions.Hosting;

namespace Conjecture.AspNetCore;

/// <summary>
/// Fluent builder that composes a <see cref="Strategy{T}"/> of <see cref="HttpInteraction"/>
/// from the endpoints discovered in a running <see cref="IHost"/>.
/// </summary>
public sealed class AspNetCoreRequestBuilder
{
    private readonly IHost host;
    private readonly HttpClient client;
    private readonly ImmutableList<Func<DiscoveredEndpoint, bool>> exclusions;
    private readonly Func<Task>? setup;
    private readonly RequestFlavour flavour;

    private enum RequestFlavour
    {
        Both,
        ValidOnly,
        MalformedOnly,
    }

    /// <summary>
    /// Discovers endpoints from <paramref name="host"/> and sends generated requests via <paramref name="client"/>.
    /// Both arguments must be non-null and outlive the builder.
    /// </summary>
    /// <param name="host">The running application host used to discover endpoints.</param>
    /// <param name="client">The HTTP client used to send generated requests.</param>
    public AspNetCoreRequestBuilder(IHost host, HttpClient client)
        : this(host, client, ImmutableList<Func<DiscoveredEndpoint, bool>>.Empty, null, RequestFlavour.Both)
    {
    }

    private AspNetCoreRequestBuilder(
        IHost host,
        HttpClient client,
        ImmutableList<Func<DiscoveredEndpoint, bool>> exclusions,
        Func<Task>? setup,
        RequestFlavour flavour)
    {
        this.host = host;
        this.client = client;
        this.exclusions = exclusions;
        this.setup = setup;
        this.flavour = flavour;
    }

    /// <summary>
    /// Returns a new builder with an additional endpoint exclusion predicate.
    /// All registered predicates are AND'd together during endpoint filtering.
    /// </summary>
    public AspNetCoreRequestBuilder ExcludeEndpoints(Func<DiscoveredEndpoint, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new(this.host, this.client, this.exclusions.Add(predicate), this.setup, this.flavour);
    }

    /// <summary>
    /// Returns a new builder with a setup delegate that runs before each generated example.
    /// </summary>
    public AspNetCoreRequestBuilder WithSetup(Func<Task> setupDelegate)
    {
        ArgumentNullException.ThrowIfNull(setupDelegate);
        return new(this.host, this.client, this.exclusions, setupDelegate, this.flavour);
    }

    /// <summary>Returns a new builder that only generates well-formed interactions.</summary>
    public AspNetCoreRequestBuilder ValidRequestsOnly()
        => new(this.host, this.client, this.exclusions, this.setup, RequestFlavour.ValidOnly);

    /// <summary>Returns a new builder that only generates malformed interactions.</summary>
    public AspNetCoreRequestBuilder MalformedRequestsOnly()
        => new(this.host, this.client, this.exclusions, this.setup, RequestFlavour.MalformedOnly);

    /// <summary>Builds the <see cref="Strategy{T}"/> of <see cref="HttpInteraction"/>.</summary>
    public Strategy<HttpInteraction> Build()
    {
        IReadOnlyList<DiscoveredEndpoint> allEndpoints = new DualEndpointWalker(this.host).Discover();

        IReadOnlyList<DiscoveredEndpoint> endpoints = this.exclusions.Count == 0
            ? allEndpoints
            : allEndpoints.Where(ep => !this.exclusions.Any(pred => pred(ep))).ToList();

        if (endpoints.Count == 0)
        {
            throw new InvalidOperationException(
                "No endpoints remain after applying exclusion predicates.");
        }

        Strategy<HttpInteraction> inner = BuildInnerStrategy(endpoints);

        if (this.setup is null)
        {
            return inner;
        }

        Func<Task> capturedSetup = this.setup;

        return Generate.Compose<HttpInteraction>(ctx =>
        {
            capturedSetup().GetAwaiter().GetResult();
            return ctx.Generate(inner);
        });
    }

    private Strategy<HttpInteraction> BuildInnerStrategy(IReadOnlyList<DiscoveredEndpoint> endpoints)
    {
        return this.flavour switch
        {
            RequestFlavour.ValidOnly => TryBuildValidStrategy(endpoints) ?? throw new InvalidOperationException(
                "No endpoints can produce valid requests. Register body parameter types with [Arbitrary] or GenForRegistry."),
            RequestFlavour.MalformedOnly => BuildMalformedStrategy(endpoints),
            _ => BuildMixedStrategy(endpoints),
        };
    }

    private static Strategy<HttpInteraction>? TryBuildValidStrategy(IReadOnlyList<DiscoveredEndpoint> endpoints)
    {
        List<Strategy<HttpInteraction>> strategies = [];

        foreach (DiscoveredEndpoint ep in endpoints)
        {
            strategies.Add(new RequestSynthesizer(ep).ValidStrategy());
        }

        return strategies.Count switch
        {
            0 => null,
            1 => strategies[0],
            _ => Generate.OneOf(strategies.ToArray()),
        };
    }

    private static Strategy<HttpInteraction> BuildMalformedStrategy(IReadOnlyList<DiscoveredEndpoint> endpoints)
    {
        Strategy<HttpInteraction>[] strategies = endpoints
            .Select(static ep => new RequestSynthesizer(ep).MalformedStrategy())
            .ToArray();

        return strategies.Length == 1 ? strategies[0] : Generate.OneOf(strategies);
    }

    private static Strategy<HttpInteraction> BuildMixedStrategy(IReadOnlyList<DiscoveredEndpoint> endpoints)
    {
        Strategy<HttpInteraction>? validStrategy = TryBuildValidStrategy(endpoints);
        Strategy<HttpInteraction> malformedStrategy = BuildMalformedStrategy(endpoints);

        if (validStrategy is null)
        {
            return malformedStrategy;
        }

        // 70% valid, 30% malformed — use 10 equally-weighted arms (7 valid + 3 malformed).
        Strategy<HttpInteraction>[] arms = new Strategy<HttpInteraction>[10];
        for (int i = 0; i < 7; i++)
        {
            arms[i] = validStrategy;
        }

        for (int i = 7; i < 10; i++)
        {
            arms[i] = malformedStrategy;
        }

        return Generate.OneOf(arms);
    }
}