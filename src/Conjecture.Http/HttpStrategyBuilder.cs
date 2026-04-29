// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Core;

namespace Conjecture.Http;

/// <summary>
/// Fluent builder for <see cref="Strategy{T}"/> of <see cref="HttpInteraction"/>.
/// Entry point: <c>Strategy.Http(resourceName)</c>. Terminal: <see cref="Build"/>.
/// </summary>
public sealed class HttpStrategyBuilder
{
    private readonly string resourceName;
    private readonly string? method;
    private readonly string? path;
    private readonly object? literalBody;
    private readonly Strategy<object?>? bodyStrategy;
    private readonly IReadOnlyDictionary<string, string>? headers;

    internal HttpStrategyBuilder(string resourceName)
        : this(resourceName, null, null, null, null, null)
    {
    }

    private HttpStrategyBuilder(
        string resourceName,
        string? method,
        string? path,
        object? literalBody,
        Strategy<object?>? bodyStrategy,
        IReadOnlyDictionary<string, string>? headers)
    {
        this.resourceName = resourceName;
        this.method = method;
        this.path = path;
        this.literalBody = literalBody;
        this.bodyStrategy = bodyStrategy;
        this.headers = headers;
    }

    /// <summary>Sets the request method to <c>GET</c> with the given <paramref name="path"/>.</summary>
    public HttpStrategyBuilder Get(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new HttpStrategyBuilder(this.resourceName, "GET", path, null, this.bodyStrategy, this.headers);
    }

    /// <summary>Sets the request method to <c>POST</c> with the given <paramref name="path"/> and optional literal <paramref name="body"/>.</summary>
    public HttpStrategyBuilder Post(string path, object? body = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new HttpStrategyBuilder(this.resourceName, "POST", path, body, this.bodyStrategy, this.headers);
    }

    /// <summary>Sets the request method to <c>PUT</c> with the given <paramref name="path"/> and optional literal <paramref name="body"/>.</summary>
    public HttpStrategyBuilder Put(string path, object? body = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new HttpStrategyBuilder(this.resourceName, "PUT", path, body, this.bodyStrategy, this.headers);
    }

    /// <summary>Sets the request method to <c>DELETE</c> with the given <paramref name="path"/>.</summary>
    public HttpStrategyBuilder Delete(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new HttpStrategyBuilder(this.resourceName, "DELETE", path, null, this.bodyStrategy, this.headers);
    }

    /// <summary>Sets the request method to <c>PATCH</c> with the given <paramref name="path"/> and optional literal <paramref name="body"/>.</summary>
    public HttpStrategyBuilder Patch(string path, object? body = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new HttpStrategyBuilder(this.resourceName, "PATCH", path, body, this.bodyStrategy, this.headers);
    }

    /// <summary>Attaches <paramref name="headers"/> to the generated interaction.</summary>
    public HttpStrategyBuilder WithHeaders(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        return new HttpStrategyBuilder(this.resourceName, this.method, this.path, this.literalBody, this.bodyStrategy, headers);
    }

    /// <summary>Overrides the target resource name.</summary>
    public HttpStrategyBuilder WithResource(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new HttpStrategyBuilder(name, this.method, this.path, this.literalBody, this.bodyStrategy, this.headers);
    }

    /// <summary>
    /// Attaches a <see cref="Strategy{T}"/> used to generate the request body.
    /// Overrides any literal body supplied via <see cref="Post"/>, <see cref="Put"/>, or <see cref="Patch"/>.
    /// </summary>
    public HttpStrategyBuilder WithBodyStrategy(Strategy<object?> bodyStrategy)
    {
        ArgumentNullException.ThrowIfNull(bodyStrategy);
        return new HttpStrategyBuilder(this.resourceName, this.method, this.path, this.literalBody, bodyStrategy, this.headers);
    }

    /// <summary>Builds a <see cref="Strategy{T}"/> producing <see cref="HttpInteraction"/>s.</summary>
    public Strategy<HttpInteraction> Build()
    {
        if (this.method is null || this.path is null)
        {
            throw new InvalidOperationException(
                "An HTTP method (Get/Post/Put/Delete/Patch) must be chosen before calling Build().");
        }

        string capturedResource = this.resourceName;
        string capturedMethod = this.method;
        string capturedPath = this.path;
        object? capturedLiteralBody = this.literalBody;
        Strategy<object?>? capturedBodyStrategy = this.bodyStrategy;
        IReadOnlyDictionary<string, string>? capturedHeaders = this.headers;

        return Strategy.Compose<HttpInteraction>(ctx =>
        {
            object? body = capturedBodyStrategy is not null
                ? ctx.Generate(capturedBodyStrategy)
                : capturedLiteralBody;
            return new HttpInteraction(capturedResource, capturedMethod, capturedPath, body, capturedHeaders);
        });
    }
}