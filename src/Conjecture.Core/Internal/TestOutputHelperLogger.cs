// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conjecture.Core.Internal;

internal sealed class TestOutputHelperLogger(Action<string> writeLine, LogLevel minLevel = LogLevel.Information) : ILogger
{
    internal static ILogger FromWriteLine(Action<string>? writeLine, LogLevel minLevel = LogLevel.Information)
    {
        return writeLine is null ? NullLogger.Instance : new TestOutputHelperLogger(writeLine, minLevel);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= minLevel;
    }

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

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullLogger.Instance.BeginScope(state)!;
    }
}
