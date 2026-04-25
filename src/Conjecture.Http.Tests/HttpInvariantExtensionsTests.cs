// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Conjecture.Http.Tests;

public class HttpInvariantExtensionsTests
{
    private sealed class StaticHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    private sealed class StaticHttpTarget(HttpClient client) : IHttpTarget
    {
        public HttpClient ResolveClient(string resourceName)
        {
            return client;
        }
    }

    private static HttpClient ClientReturning(HttpStatusCode status, string? body = null, string? mediaType = null)
    {
        HttpResponseMessage response = new(status);
        if (body is not null)
        {
            response.Content = new StringContent(body, Encoding.UTF8, mediaType ?? "application/json");
        }

        return new HttpClient(new StaticHandler(response)) { BaseAddress = new Uri("http://localhost/") };
    }

    [Fact]
    public async Task AssertNot5xx_ReturnsSuccessfullyFor2xx()
    {
        HttpResponseMessage ok = new(HttpStatusCode.OK);
        Task<HttpResponseMessage> task = Task.FromResult(ok);
        await task.AssertNot5xx();
    }

    [Fact]
    public async Task AssertNot5xx_ReturnsSuccessfullyFor4xx()
    {
        HttpResponseMessage notFound = new(HttpStatusCode.NotFound);
        Task<HttpResponseMessage> task = Task.FromResult(notFound);
        await task.AssertNot5xx();
    }

    [Fact]
    public async Task AssertNot5xx_ThrowsFor500()
    {
        HttpResponseMessage err = new(HttpStatusCode.InternalServerError);
        Task<HttpResponseMessage> task = Task.FromResult(err);
        await Assert.ThrowsAnyAsync<Exception>(async () => await task.AssertNot5xx());
    }

    [Fact]
    public async Task AssertNot5xx_ThrowsFor503()
    {
        HttpResponseMessage err = new(HttpStatusCode.ServiceUnavailable);
        Task<HttpResponseMessage> task = Task.FromResult(err);
        await Assert.ThrowsAnyAsync<Exception>(async () => await task.AssertNot5xx());
    }

    [Fact]
    public async Task Assert4xx_ReturnsSuccessfullyFor404()
    {
        HttpResponseMessage notFound = new(HttpStatusCode.NotFound);
        Task<HttpResponseMessage> task = Task.FromResult(notFound);
        await task.Assert4xx();
    }

    [Fact]
    public async Task Assert4xx_ReturnsSuccessfullyFor400()
    {
        HttpResponseMessage bad = new(HttpStatusCode.BadRequest);
        Task<HttpResponseMessage> task = Task.FromResult(bad);
        await task.Assert4xx();
    }

    [Fact]
    public async Task Assert4xx_ThrowsFor200()
    {
        HttpResponseMessage ok = new(HttpStatusCode.OK);
        Task<HttpResponseMessage> task = Task.FromResult(ok);
        await Assert.ThrowsAnyAsync<Exception>(async () => await task.Assert4xx());
    }

    [Fact]
    public async Task Assert4xx_ThrowsFor500()
    {
        HttpResponseMessage err = new(HttpStatusCode.InternalServerError);
        Task<HttpResponseMessage> task = Task.FromResult(err);
        await Assert.ThrowsAnyAsync<Exception>(async () => await task.Assert4xx());
    }

    [Fact]
    public async Task AssertProblemDetailsShape_AcceptsBodyWithTitleField()
    {
        string body = "{\"title\":\"Not Found\"}";
        HttpResponseMessage response = new(HttpStatusCode.NotFound)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/problem+json"),
        };
        Task<HttpResponseMessage> task = Task.FromResult(response);
        await task.AssertProblemDetailsShape();
    }

    [Fact]
    public async Task AssertProblemDetailsShape_AcceptsBodyWithTypeField()
    {
        string body = "{\"type\":\"https://example.com/problem\"}";
        HttpResponseMessage response = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/problem+json"),
        };
        Task<HttpResponseMessage> task = Task.FromResult(response);
        await task.AssertProblemDetailsShape();
    }

    [Fact]
    public async Task AssertProblemDetailsShape_AcceptsBodyWithBothFields()
    {
        string body = "{\"type\":\"about:blank\",\"title\":\"Bad Request\",\"status\":400}";
        HttpResponseMessage response = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/problem+json"),
        };
        Task<HttpResponseMessage> task = Task.FromResult(response);
        await task.AssertProblemDetailsShape();
    }

    [Fact]
    public async Task AssertProblemDetailsShape_ThrowsWhenBodyLacksTypeAndTitle()
    {
        string body = "{\"detail\":\"oops\"}";
        HttpResponseMessage response = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/problem+json"),
        };
        Task<HttpResponseMessage> task = Task.FromResult(response);
        await Assert.ThrowsAnyAsync<Exception>(async () => await task.AssertProblemDetailsShape());
    }

    [Fact]
    public async Task AssertProblemDetailsShape_ThrowsOnNonJsonBody()
    {
        HttpResponseMessage response = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("not json at all", Encoding.UTF8, "text/plain"),
        };
        Task<HttpResponseMessage> task = Task.FromResult(response);
        await Assert.ThrowsAnyAsync<Exception>(async () => await task.AssertProblemDetailsShape());
    }

    [Fact]
    public async Task AssertProblemDetailsShape_ThrowsOnEmptyBody()
    {
        HttpResponseMessage response = new(HttpStatusCode.BadRequest);
        Task<HttpResponseMessage> task = Task.FromResult(response);
        await Assert.ThrowsAnyAsync<Exception>(async () => await task.AssertProblemDetailsShape());
    }

    [Fact]
    public async Task Response_ExecutesInteractionAgainstTargetAndReturnsResponse()
    {
        HttpClient client = ClientReturning(HttpStatusCode.Accepted);
        StaticHttpTarget target = new(client);
        HttpInteraction interaction = new("api", "GET", "/ping", null, null);

        HttpResponseMessage response = await interaction.Response(target);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Response_IsChainableWithAssertNot5xx()
    {
        HttpClient client = ClientReturning(HttpStatusCode.OK);
        StaticHttpTarget target = new(client);
        HttpInteraction interaction = new("api", "GET", "/ok", null, null);

        await interaction.Response(target).AssertNot5xx();
    }

    [Fact]
    public async Task Response_ChainedAssert4xx_ThrowsOn2xx()
    {
        HttpClient client = ClientReturning(HttpStatusCode.OK);
        StaticHttpTarget target = new(client);
        HttpInteraction interaction = new("api", "GET", "/ok", null, null);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await interaction.Response(target).Assert4xx());
    }

    [Fact]
    public async Task Response_ChainedAssertProblemDetailsShape_Succeeds()
    {
        HttpClient client = ClientReturning(
            HttpStatusCode.BadRequest,
            "{\"title\":\"Bad\"}",
            "application/problem+json");
        StaticHttpTarget target = new(client);
        HttpInteraction interaction = new("api", "GET", "/bad", null, null);

        await interaction.Response(target).AssertProblemDetailsShape();
    }
}