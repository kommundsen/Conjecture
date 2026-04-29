// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.IO;
using System.Reflection;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Resources;

internal static class ApiReferenceResources
{
    private static readonly IReadOnlyList<(string Topic, string Name, string Description)> Topics =
    [
        ("strategies",     "Conjecture Strategies",     "All Strategy.* factory methods and LINQ combinators"),
        ("settings",       "Conjecture Settings",        "ConjectureSettings, ConjectureSettingsAttribute, and configuration options"),
        ("state-machines", "Stateful Testing",           "IStateMachine<TState,TCommand>, StateMachineRun, and Strategy.StateMachine"),
        ("shrinking",      "Shrinking Explained",        "How Conjecture finds minimal counterexamples"),
        ("assumptions",          "Assumptions and Filtering",  "Assume.That(), IGenerationContext.Assume(), and UnsatisfiedAssumptionException"),
        ("attributes",           "Test Attributes",            "[Property], [Example], [From<T>], [FromFactory], [ConjectureSettings]"),
        ("targeted-testing",     "Targeted Testing",           "Target.Maximize, Target.Minimize, IGenerationContext.Target, and targeting settings"),
        ("recursive-strategies", "Recursive Strategies",       "Strategy.Recursive<T> for tree-shaped and self-referential types"),
        ("testing-platform",     "Microsoft Testing Platform Adapter", "Conjecture.TestingPlatform setup, [Property] attribute, CLI options, TRX reports, and CrashDump"),
        ("aspire-setup",         "Aspire Integration Setup",           "Step-by-step guide for wiring up Conjecture.Aspire: IAspireAppFixture, AspireStateMachine<TState>, ResetAsync lifecycle, and [Property] attribute"),
        ("efcore-setup",         "EF Core Integration Setup",          "Step-by-step guide for wiring up Conjecture.EFCore: Strategy.Entity, Strategy.EntitySet, RoundtripAsserter, and MigrationHarness"),
        ("aspnetcore-efcore-setup", "ASP.NET Core + EF Core Integration Setup", "Step-by-step guide for wiring up Conjecture.AspNetCore.EFCore: AspNetCoreDbTarget, AssertNoPartialWritesOnErrorAsync, AssertCascadeCorrectnessAsync, and AssertIdempotentAsync"),
    ];

    internal static ValueTask<ListResourcesResult> HandleListResources(
        RequestContext<ListResourcesRequestParams> _, CancellationToken __)
    {
        List<Resource> resources = Topics.Select(t => new Resource
        {
            Uri = $"conjecture://api/{t.Topic}",
            Name = t.Name,
            Description = t.Description,
            MimeType = "text/markdown",
        }).ToList();

        return ValueTask.FromResult(new ListResourcesResult { Resources = resources });
    }

    internal static ValueTask<ReadResourceResult> HandleReadResource(
        RequestContext<ReadResourceRequestParams> context, CancellationToken _)
    {
        string uri = context.Params?.Uri ?? string.Empty;
        const string prefix = "conjecture://api/";

        if (!uri.StartsWith(prefix, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(new ReadResourceResult
            {
                Contents = [new TextResourceContents { Uri = uri, MimeType = "text/plain", Text = $"Unknown resource URI: {uri}" }],
            });
        }

        string topic = uri[prefix.Length..];
        string text = LoadDoc(topic);

        return ValueTask.FromResult(new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = uri, MimeType = "text/markdown", Text = text }],
        });
    }

    private static string LoadDoc(string topic)
    {
        string resourceName = $"Conjecture.Mcp.Docs.{topic}.md";
        Assembly assembly = typeof(ApiReferenceResources).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            string available = string.Join(", ", Topics.Select(t => t.Topic));
            return $"Topic '{topic}' not found. Available topics: {available}";
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}