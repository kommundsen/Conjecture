// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Core;

namespace Conjecture.Messaging;

/// <summary>
/// Fluent builder for <see cref="Strategy{T}"/> of <see cref="MessageInteraction"/>.
/// Entry point: <c>Generate.Messaging</c>.
/// </summary>
public sealed class MessagingGenerateBuilder
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(0);

    private static readonly Strategy<Guid> GuidStrategy = Generate.Guids();

    internal MessagingGenerateBuilder()
    {
    }

    /// <summary>
    /// Returns a <see cref="Strategy{T}"/> that generates a publish <see cref="MessageInteraction"/>
    /// with a deterministic <see cref="MessageInteraction.MessageId"/>, empty headers, and null correlation ID.
    /// </summary>
    public Strategy<MessageInteraction> Publish(
        string destination,
        Strategy<ReadOnlyMemory<byte>> bodyStrategy)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(bodyStrategy);

        return Generate.Compose<MessageInteraction>(ctx =>
        {
            Guid messageId = ctx.Generate(GuidStrategy);
            ReadOnlyMemory<byte> body = ctx.Generate(bodyStrategy);
            return new MessageInteraction(
                destination,
                body,
                EmptyHeaders,
                messageId.ToString());
        });
    }

    /// <summary>
    /// Returns a <see cref="Strategy{T}"/> that generates a publish <see cref="MessageInteraction"/>
    /// with custom headers and correlation ID strategies.
    /// </summary>
    public Strategy<MessageInteraction> Publish(
        string destination,
        Strategy<ReadOnlyMemory<byte>> bodyStrategy,
        Strategy<IReadOnlyDictionary<string, string>> headersStrategy,
        Strategy<string?>? correlationIdStrategy = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(bodyStrategy);
        ArgumentNullException.ThrowIfNull(headersStrategy);

        return Generate.Compose<MessageInteraction>(ctx =>
        {
            Guid messageId = ctx.Generate(GuidStrategy);
            ReadOnlyMemory<byte> body = ctx.Generate(bodyStrategy);
            IReadOnlyDictionary<string, string> headers = ctx.Generate(headersStrategy);
            string? correlationId = correlationIdStrategy is not null
                ? ctx.Generate(correlationIdStrategy)
                : null;
            return new MessageInteraction(
                destination,
                body,
                headers,
                messageId.ToString(),
                correlationId);
        });
    }

    /// <summary>
    /// Returns a <see cref="Strategy{T}"/> that generates a consume marker <see cref="MessageInteraction"/>
    /// carrying only <paramref name="destination"/>; body is empty and headers are empty.
    /// </summary>
    public Strategy<MessageInteraction> Consume(string destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return Generate.Just(new MessageInteraction(
            destination,
            ReadOnlyMemory<byte>.Empty,
            EmptyHeaders,
            string.Empty));
    }
}