// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


using Conjecture.Abstractions.Interactions;

namespace Conjecture.Http.Tests;

public class HttpInteractionTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return Response;
        }
    }

    private sealed class FakeHttpTarget(HttpClient client) : IHttpTarget
    {
        public HttpClient ResolveClient(string resourceName)
        {
            return client;
        }
    }

    private sealed class RoutingHttpTarget(IReadOnlyDictionary<string, HttpClient> clients) : IHttpTarget
    {
        public HttpClient ResolveClient(string resourceName)
        {
            return clients[resourceName];
        }
    }

    [Fact]
    public void HttpInteraction_ImplementsIAddressedInteraction()
    {
        HttpInteraction interaction = new("api", "GET", "/ping", null, null);
        Assert.IsAssignableFrom<IAddressedInteraction>(interaction);
        Assert.IsAssignableFrom<IInteraction>(interaction);
    }

    [Fact]
    public void HttpInteraction_ResourceName_ExposedViaInterface()
    {
        HttpInteraction interaction = new("orders", "GET", "/", null, null);
        IAddressedInteraction addressed = interaction;
        Assert.Equal("orders", addressed.ResourceName);
    }

    [Fact]
    public void HttpInteraction_IsReadonlyRecordStruct()
    {
        Type type = typeof(HttpInteraction);
        Assert.True(type.IsValueType);
        Assert.True(type.GetCustomAttributes(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), false).Length > 0);
    }

    [Fact]
    public async Task ExecuteAsync_GetRequest_DispatchesThroughResolvedClient()
    {
        CapturingHandler handler = new();
        HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost/") };
        IInteractionTarget target = new FakeHttpTarget(client);
        HttpInteraction interaction = new("api", "GET", "/ping", null, null);

        object? result = await target.ExecuteAsync(interaction, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal(new Uri("http://localhost/ping"), handler.LastRequest.RequestUri);
        HttpResponseMessage response = Assert.IsType<HttpResponseMessage>(result);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_PostWithHttpContentBody_SendsBody()
    {
        CapturingHandler handler = new();
        HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost/") };
        IInteractionTarget target = new FakeHttpTarget(client);
        StringContent body = new("{\"x\":1}", Encoding.UTF8, "application/json");
        HttpInteraction interaction = new("api", "POST", "/items", body, null);

        await target.ExecuteAsync(interaction, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("{\"x\":1}", handler.LastBody);
    }

    [Fact]
    public async Task ExecuteAsync_Headers_AreAppliedToRequest()
    {
        CapturingHandler handler = new();
        HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost/") };
        IInteractionTarget target = new FakeHttpTarget(client);
        Dictionary<string, string> headers = new()
        {
            ["X-Trace-Id"] = "abc123",
        };
        HttpInteraction interaction = new("api", "GET", "/", null, headers);

        await target.ExecuteAsync(interaction, CancellationToken.None);

        Assert.True(handler.LastRequest!.Headers.TryGetValues("X-Trace-Id", out IEnumerable<string>? values));
        Assert.Contains("abc123", values!);
    }

    [Fact]
    public async Task ExecuteAsync_UsesResourceNameToResolveClient()
    {
        CapturingHandler handlerA = new() { Response = new HttpResponseMessage(HttpStatusCode.OK) };
        CapturingHandler handlerB = new() { Response = new HttpResponseMessage(HttpStatusCode.Accepted) };
        HttpClient clientA = new(handlerA) { BaseAddress = new Uri("http://a/") };
        HttpClient clientB = new(handlerB) { BaseAddress = new Uri("http://b/") };
        Dictionary<string, HttpClient> clients = new()
        {
            ["a"] = clientA,
            ["b"] = clientB,
        };
        IInteractionTarget target = new RoutingHttpTarget(clients);

        HttpInteraction toA = new("a", "GET", "/", null, null);
        HttpInteraction toB = new("b", "GET", "/", null, null);

        object? resA = await target.ExecuteAsync(toA, CancellationToken.None);
        object? resB = await target.ExecuteAsync(toB, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, ((HttpResponseMessage)resA!).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, ((HttpResponseMessage)resB!).StatusCode);
        Assert.NotNull(handlerA.LastRequest);
        Assert.NotNull(handlerB.LastRequest);
    }

    [Fact]
    public async Task ExecuteAsync_NonHttpInteraction_Throws()
    {
        CapturingHandler handler = new();
        HttpClient client = new(handler);
        IInteractionTarget target = new FakeHttpTarget(client);
        NotHttp interaction = new();

        await Assert.ThrowsAnyAsync<Exception>(
            async () => await target.ExecuteAsync(interaction, CancellationToken.None));
    }

    private sealed class NotHttp : IInteraction { }
}