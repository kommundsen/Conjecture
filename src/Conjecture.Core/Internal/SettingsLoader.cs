using System.Text.Json;

namespace Conjecture.Core.Internal;

internal static class SettingsLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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
