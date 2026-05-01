// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


using Conjecture.Abstractions.Interactions;

namespace Conjecture.Http;

/// <summary>
/// Fluent invariant assertions for HTTP responses produced by <see cref="HttpInteraction"/>.
/// </summary>
/// <remarks>
/// All assertions accept <see cref="Task{HttpResponseMessage}"/> so they compose directly
/// on the result of <see cref="Response(HttpInteraction, IHttpTarget, CancellationToken)"/>.
/// Failures throw <see cref="HttpInvariantException"/> with a message that captures the
/// minimum failing response (status + a short body excerpt) to aid shrinking.
/// </remarks>
public static class HttpInvariantExtensions
{
    /// <summary>Asserts the response status is not in the 5xx range.</summary>
    public static async Task AssertNot5xx(this Task<HttpResponseMessage> response)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        HttpResponseMessage result = await response.ConfigureAwait(false);
        int status = (int)result.StatusCode;
        if (status >= 500 && status <= 599)
        {
            string body = await ReadBodyExcerptAsync(result).ConfigureAwait(false);
            throw new HttpInvariantException(
                $"Expected non-5xx response but got {status} {result.ReasonPhrase}. Body: {body}");
        }
    }

    /// <summary>Asserts the response status is in the 4xx range.</summary>
    public static async Task Assert4xx(this Task<HttpResponseMessage> response)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        HttpResponseMessage result = await response.ConfigureAwait(false);
        int status = (int)result.StatusCode;
        if (status < 400 || status > 499)
        {
            string body = await ReadBodyExcerptAsync(result).ConfigureAwait(false);
            throw new HttpInvariantException(
                $"Expected 4xx response but got {status} {result.ReasonPhrase}. Body: {body}");
        }
    }

    /// <summary>
    /// Asserts the response body is a JSON object with a <c>type</c> or <c>title</c> field,
    /// matching the minimum shape required by RFC 7807 <c>application/problem+json</c>.
    /// </summary>
    public static async Task AssertProblemDetailsShape(this Task<HttpResponseMessage> response)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        HttpResponseMessage result = await response.ConfigureAwait(false);
        string body = result.Content is null
            ? string.Empty
            : await result.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new HttpInvariantException(
                $"Expected RFC 7807 problem+json body but response body was empty (status {(int)result.StatusCode}).");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            string excerpt = Excerpt(body);
            throw new HttpInvariantException(
                $"Expected RFC 7807 problem+json body but failed to parse JSON: {ex.Message}. Body: {excerpt}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new HttpInvariantException(
                    $"Expected RFC 7807 problem+json object but got {document.RootElement.ValueKind}. Body: {Excerpt(body)}");
            }

            bool hasType = document.RootElement.TryGetProperty("type", out JsonElement _);
            bool hasTitle = document.RootElement.TryGetProperty("title", out JsonElement _);
            if (!hasType && !hasTitle)
            {
                throw new HttpInvariantException(
                    $"Expected RFC 7807 problem+json body with 'type' or 'title' field. Body: {Excerpt(body)}");
            }
        }
    }

    /// <summary>
    /// Executes <paramref name="interaction"/> against <paramref name="target"/> and returns the response.
    /// Designed for fluent chaining with the <c>Assert*</c> invariants.
    /// </summary>
    public static async Task<HttpResponseMessage> Response(
        this HttpInteraction interaction,
        IHttpTarget target,
        CancellationToken cancellationToken = default)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        IInteractionTarget interactionTarget = target;
        object? result = await interactionTarget.ExecuteAsync(interaction, cancellationToken).ConfigureAwait(false);
        return result is not HttpResponseMessage response
            ? throw new HttpInvariantException(
                $"Expected {nameof(HttpResponseMessage)} from {nameof(IHttpTarget)} but got {result?.GetType().Name ?? "null"}.")
            : response;
    }

    private static async Task<string> ReadBodyExcerptAsync(HttpResponseMessage response)
    {
        if (response.Content is null)
        {
            return "<empty>";
        }

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return Excerpt(body);
    }

    private static string Excerpt(string body)
    {
        const int max = 200;
        return string.IsNullOrEmpty(body) ? "<empty>" : body.Length <= max ? body : body[..max] + "…";
    }
}