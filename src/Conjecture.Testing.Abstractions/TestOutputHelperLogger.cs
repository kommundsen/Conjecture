// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.ComponentModel;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conjecture.Abstractions.Testing;

/// <summary>An <see cref="ILogger"/> implementation that forwards messages to a test-framework write-line callback.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TestOutputHelperLogger(Action<string> writeLine, LogLevel minLevel = LogLevel.Information) : ILogger
{
    /// <summary>Returns an <see cref="ILogger"/> that forwards to <paramref name="writeLine"/>, or a null logger when <paramref name="writeLine"/> is null.</summary>
    public static ILogger FromWriteLine(Action<string>? writeLine, LogLevel minLevel = LogLevel.Information)
    {
        return writeLine is null ? NullLogger.Instance : new TestOutputHelperLogger(writeLine, minLevel);
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= minLevel;
    }

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        writeLine($"[{logLevel}] {message}");
    }

    /// <inheritdoc/>
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullLogger.Instance.BeginScope(state)!;
    }
}
