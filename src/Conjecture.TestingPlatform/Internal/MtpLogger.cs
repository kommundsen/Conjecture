// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.Extensions.Logging;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Conjecture.TestingPlatform.Internal;

internal sealed class MtpLogger : ILogger
{
    private readonly IDataProducer? producer;
    private readonly IMessageBus bus;
    private readonly TestNodeUid nodeUid;
    private readonly SessionUid session;

    internal MtpLogger(IMessageBus bus, TestNodeUid nodeUid, SessionUid session)
        : this(null, bus, nodeUid, session)
    {
    }

    internal MtpLogger(IDataProducer? producer, IMessageBus bus, TestNodeUid nodeUid, SessionUid session)
    {
        this.producer = producer;
        this.bus = bus;
        this.nodeUid = nodeUid;
        this.session = session;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel level)
    {
        return level >= LogLevel.Information;
    }

    public void Log<TState>(
        LogLevel level,
        EventId id,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        string message = formatter(state, exception);
        IProperty property = level is LogLevel.Error or LogLevel.Critical
            ? new StandardErrorProperty(message + Environment.NewLine)
            : new StandardOutputProperty($"[{level}] {message}{Environment.NewLine}");
        _ = bus.PublishAsync(producer!, new TestNodeUpdateMessage(
            session,
            new TestNode
            {
                Uid = nodeUid,
                DisplayName = string.Empty,
                Properties = new PropertyBag(property)
            }));
    }
}