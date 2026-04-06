// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Conjecture.Core.Internal;

internal static class SettingsLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [RequiresUnreferencedCode("JSON deserialization of SettingsDto requires type metadata preserved at runtime.")]
    internal static ConjectureSettings Load(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, ".conjecture", "settings.json");

        if (!File.Exists(path))
        {
            return new ConjectureSettings();
        }

        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<SettingsDto>(json, Options)!;
        var defaults = new ConjectureSettings();

        return new ConjectureSettings
        {
            MaxExamples = dto.MaxExamples ?? defaults.MaxExamples,
            Seed = dto.Seed,
            UseDatabase = dto.UseDatabase ?? defaults.UseDatabase,
            Deadline = dto.Deadline,
            MaxStrategyRejections = dto.MaxStrategyRejections ?? defaults.MaxStrategyRejections,
            MaxUnsatisfiedRatio = dto.MaxUnsatisfiedRatio ?? defaults.MaxUnsatisfiedRatio,
            DatabasePath = dto.DatabasePath ?? defaults.DatabasePath,
        };
    }

    private sealed class SettingsDto
    {
        public int? MaxExamples { get; init; }
        public ulong? Seed { get; init; }
        public bool? UseDatabase { get; init; }
        public TimeSpan? Deadline { get; init; }
        public int? MaxStrategyRejections { get; init; }
        public int? MaxUnsatisfiedRatio { get; init; }
        public string? DatabasePath { get; init; }
    }
}