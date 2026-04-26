// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Aspire;

namespace Conjecture.Aspire.Tests;

public class InteractionTraceReporterTests
{
    // ── FormatReport includes all recorded interactions in order ────────────────

    [Fact]
    public async Task FormatReport_WithMultipleInteractions_IncludesAllInOrder()
    {
        InteractionTraceReporter reporter = new();
        Interaction first = new("order-service", "POST", "/orders", """{"productId":42}""");
        Interaction second = new("order-service", "GET", "/orders/abc", null);

        using HttpResponseMessage firstResponse = new(HttpStatusCode.Created);
        firstResponse.Content = new StringContent("""{"id":"abc"}""");
        using HttpResponseMessage secondResponse = new(HttpStatusCode.OK);
        secondResponse.Content = new StringContent("""{"status":"pending"}""");

        await reporter.Record(first, firstResponse, System.TimeSpan.FromMilliseconds(50));
        await reporter.Record(second, secondResponse, System.TimeSpan.FromMilliseconds(30));

        string report = reporter.FormatReport(null);

        int indexFirst = report.IndexOf("/orders", System.StringComparison.Ordinal);
        int indexSecond = report.IndexOf("/orders/abc", System.StringComparison.Ordinal);
        Assert.True(indexFirst < indexSecond);
    }

    // ── Each entry shows method, resource name, path, request body ──────────────

    [Fact]
    public async Task FormatReport_WithBody_IncludesMethodResourcePathAndBody()
    {
        InteractionTraceReporter reporter = new();
        Interaction interaction = new("order-service", "POST", "/orders", """{"productId":42,"qty":1}""");

        using HttpResponseMessage response = new(HttpStatusCode.Created);
        response.Content = new StringContent("""{"id":"abc"}""");

        await reporter.Record(interaction, response, System.TimeSpan.FromMilliseconds(10));

        string report = reporter.FormatReport(null);

        Assert.Contains("POST", report, System.StringComparison.Ordinal);
        Assert.Contains("order-service", report, System.StringComparison.Ordinal);
        Assert.Contains("/orders", report, System.StringComparison.Ordinal);
        Assert.Contains("""{"productId":42,"qty":1}""", report, System.StringComparison.Ordinal);
    }

    // ── Each entry shows response status and response body ──────────────────────

    [Fact]
    public async Task FormatReport_RecordedInteraction_IncludesResponseStatusAndBody()
    {
        InteractionTraceReporter reporter = new();
        Interaction interaction = new("order-service", "GET", "/orders/abc", null);

        using HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Content = new StringContent("""{"status":"pending"}""");

        await reporter.Record(interaction, response, System.TimeSpan.FromMilliseconds(10));

        string report = reporter.FormatReport(null);

        Assert.Contains("200", report, System.StringComparison.Ordinal);
        Assert.Contains("""{"status":"pending"}""", report, System.StringComparison.Ordinal);
    }

    // ── Final failing step is annotated ─────────────────────────────────────────

    [Fact]
    public async Task FormatReport_FinalStep_IsAnnotatedWithInvariantViolated()
    {
        InteractionTraceReporter reporter = new();
        Interaction first = new("svc", "POST", "/items", null);
        Interaction second = new("svc", "GET", "/items/1", null);

        using HttpResponseMessage firstResponse = new(HttpStatusCode.Created);
        firstResponse.Content = new StringContent("""{"id":"1"}""");
        using HttpResponseMessage secondResponse = new(HttpStatusCode.OK);
        secondResponse.Content = new StringContent("""{"status":"bad"}""");

        await reporter.Record(first, firstResponse, System.TimeSpan.FromMilliseconds(10));
        await reporter.Record(second, secondResponse, System.TimeSpan.FromMilliseconds(10));

        string report = reporter.FormatReport(null);

        int annotationIndex = report.LastIndexOf("invariant violated", System.StringComparison.OrdinalIgnoreCase);
        int secondStepIndex = report.IndexOf("/items/1", System.StringComparison.Ordinal);
        Assert.True(annotationIndex > secondStepIndex);
    }

    // ── Service log section is included after the trace ─────────────────────────

    [Fact]
    public async Task FormatReport_WithNullApp_IncludesServiceLogSection()
    {
        InteractionTraceReporter reporter = new();
        Interaction interaction = new("order-service", "GET", "/health", null);

        using HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Content = new StringContent("OK");

        await reporter.Record(interaction, response, System.TimeSpan.FromMilliseconds(5));

        string report = reporter.FormatReport(null);

        Assert.Contains("Service logs", report, System.StringComparison.OrdinalIgnoreCase);
    }

    // ── Empty trace produces a descriptive message ───────────────────────────────

    [Fact]
    public void FormatReport_EmptyTrace_ProducesDescriptiveMessage()
    {
        InteractionTraceReporter reporter = new();

        string report = reporter.FormatReport(null);

        Assert.False(string.IsNullOrWhiteSpace(report));
    }
}