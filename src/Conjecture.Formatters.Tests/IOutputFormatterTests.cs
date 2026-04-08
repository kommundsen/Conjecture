// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Formatters.Tests;

public class IOutputFormatterTests
{
    [Fact]
    public async Task WriteAsync_CanBeImplementedByCustomType()
    {
        var formatter = new FakeFormatter();
        using var stream = new MemoryStream();

        await formatter.WriteAsync([1, 2, 3], stream);

        Assert.Equal(1, formatter.CallCount);
    }

    private sealed class FakeFormatter : IOutputFormatter
    {
        public int CallCount { get; private set; }
        public string Name => "fake";

        public Task WriteAsync<T>(IEnumerable<T> data, Stream output, CancellationToken ct = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
