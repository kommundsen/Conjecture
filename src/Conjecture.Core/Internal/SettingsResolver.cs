namespace Conjecture.Core.Internal;

internal static class SettingsResolver
{
    internal static ConjectureSettings Resolve(
        string baseDirectory,
        ConjectureSettingsAttribute? assemblyAttribute = null,
        ConjectureSettings? testLevel = null)
    {
        if (testLevel != null)
        {
            return testLevel;
        }
        var fromJson = SettingsLoader.Load(baseDirectory);
        return assemblyAttribute?.Apply(fromJson) ?? fromJson;
    }
}
