// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Conjecture.TestingPlatform.Internal;

internal sealed class ConjectureCommandLineOptions : ICommandLineOptionsProvider
{
    internal const string SeedOption = "conjecture-seed";
    internal const string MaxExamplesOption = "conjecture-max-examples";

    public string Uid => "Conjecture.CommandLine";
    public string DisplayName => "Conjecture options";
    public string Description => "CLI options for Conjecture property-based testing";
    public string Version => typeof(ConjectureCommandLineOptions).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";

    public Task<bool> IsEnabledAsync()
    {
        return Task.FromResult(true);
    }

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
    {
        return
        [
            new(SeedOption, "Override the random seed for all property runs", ArgumentArity.ExactlyOne, false),
            new(MaxExamplesOption, "Override MaxExamples for all property runs", ArgumentArity.ExactlyOne, false),
        ];
    }

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption option, string[] args)
    {
        return option.Name switch
        {
            SeedOption => ulong.TryParse(args[0], out _)
                ? Task.FromResult(ValidationResult.Valid())
                : Task.FromResult(ValidationResult.Invalid("--conjecture-seed must be a non-negative integer")),
            MaxExamplesOption => int.TryParse(args[0], out int v) && v > 0
                ? Task.FromResult(ValidationResult.Valid())
                : Task.FromResult(ValidationResult.Invalid("--conjecture-max-examples must be a positive integer")),
            _ => Task.FromResult(ValidationResult.Valid()),
        };
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions options)
    {
        return Task.FromResult(ValidationResult.Valid());
    }
}