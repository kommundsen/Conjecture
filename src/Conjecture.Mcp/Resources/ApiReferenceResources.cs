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
        ("strategies",     "Conjecture Strategies",     "All Generate.* factory methods and LINQ combinators"),
        ("settings",       "Conjecture Settings",        "ConjectureSettings, ConjectureSettingsAttribute, and configuration options"),
        ("state-machines", "Stateful Testing",           "IStateMachine<TState,TCommand>, StateMachineRun, and Generate.StateMachine"),
        ("shrinking",      "Shrinking Explained",        "How Conjecture finds minimal counterexamples"),
        ("assumptions",          "Assumptions and Filtering",  "Assume.That(), IGeneratorContext.Assume(), and UnsatisfiedAssumptionException"),
        ("attributes",           "Test Attributes",            "[Property], [Example], [From<T>], [FromFactory], [ConjectureSettings]"),
        ("targeted-testing",     "Targeted Testing",           "Target.Maximize, Target.Minimize, IGeneratorContext.Target, and targeting settings"),
        ("recursive-strategies", "Recursive Strategies",       "Generate.Recursive<T> for tree-shaped and self-referential types"),
    ];

    internal static ValueTask<ListResourcesResult> HandleListResources(
        RequestContext<ListResourcesRequestParams> context, CancellationToken cancellationToken)
    {
        var resources = Topics.Select(t => new Resource
        {
            Uri = $"conjecture://api/{t.Topic}",
            Name = t.Name,
            Description = t.Description,
            MimeType = "text/markdown",
        }).ToList();

        return ValueTask.FromResult(new ListResourcesResult { Resources = resources });
    }

    internal static ValueTask<ReadResourceResult> HandleReadResource(
        RequestContext<ReadResourceRequestParams> context, CancellationToken cancellationToken)
    {
        var uri = context.Params?.Uri ?? string.Empty;
        const string prefix = "conjecture://api/";

        if (!uri.StartsWith(prefix, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(new ReadResourceResult
            {
                Contents = [new TextResourceContents { Uri = uri, MimeType = "text/plain", Text = $"Unknown resource URI: {uri}" }],
            });
        }

        var topic = uri[prefix.Length..];
        var text = LoadDoc(topic);

        return ValueTask.FromResult(new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = uri, MimeType = "text/markdown", Text = text }],
        });
    }

    private static string LoadDoc(string topic)
    {
        var resourceName = $"Conjecture.Mcp.Docs.{topic}.md";
        var assembly = typeof(ApiReferenceResources).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            var available = string.Join(", ", Topics.Select(t => t.Topic));
            return $"Topic '{topic}' not found. Available topics: {available}";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
