// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Interactions;
using Conjecture.Messaging;

namespace Conjecture.Messaging.Tests;

public class MessageInteractionTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new Dictionary<string, string>(0);

    [Fact]
    public void MessageInteraction_ImplementsIInteraction()
    {
        MessageInteraction msg = new(
            "orders",
            ReadOnlyMemory<byte>.Empty,
            EmptyHeaders,
            "msg-1");

        Assert.IsAssignableFrom<IInteraction>(msg);
    }

    [Fact]
    public void MessageInteraction_Properties_RoundTrip()
    {
        byte[] bodyBytes = [1, 2, 3];
        Dictionary<string, string> headers = new() { ["content-type"] = "application/json" };

        MessageInteraction msg = new(
            "invoices",
            bodyBytes,
            headers,
            "msg-42",
            "corr-7");

        Assert.Equal("invoices", msg.Destination);
        Assert.Equal(bodyBytes, msg.Body.ToArray());
        Assert.Equal("application/json", msg.Headers["content-type"]);
        Assert.Equal("msg-42", msg.MessageId);
        Assert.Equal("corr-7", msg.CorrelationId);
    }

    [Fact]
    public void MessageInteraction_CorrelationId_DefaultsToNull()
    {
        MessageInteraction msg = new(
            "queue",
            ReadOnlyMemory<byte>.Empty,
            EmptyHeaders,
            "msg-99");

        Assert.Null(msg.CorrelationId);
    }

    [Fact]
    public void MessageInteraction_IsImmutableRecord_EqualityByValue()
    {
        MessageInteraction a = new("q", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "id-1");
        MessageInteraction b = new("q", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "id-1");

        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData("")]
    [InlineData("orders")]
    [InlineData("topic/sub-topic")]
    public void MessageInteraction_Destination_AcceptsVariousNames(string destination)
    {
        MessageInteraction msg = new(
            destination,
            ReadOnlyMemory<byte>.Empty,
            EmptyHeaders,
            "id");

        Assert.Equal(destination, msg.Destination);
    }
}