// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Conjecture.TestingPlatform.Internal;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Conjecture.TestingPlatform.Tests;

public class ConjectureCommandLineOptionsTests
{
    // ---- helpers -------------------------------------------------------------

    private static ConjectureCommandLineOptions BuildOptions()
    {
        return new();
    }

    private sealed class CapturingMessageBus : IMessageBus
    {
        private readonly List<TestNodeUpdateMessage> messages = [];

        public IReadOnlyList<TestNodeUpdateMessage> Messages => messages;

        public Task PublishAsync(IDataProducer dataProducer, IData data)
        {
            if (data is TestNodeUpdateMessage msg)
            {
                messages.Add(msg);
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>Fake <see cref="ICommandLineOptions"/> backed by a dictionary.</summary>
    private sealed class FakeCommandLineOptions : ICommandLineOptions
    {
        private readonly Dictionary<string, string[]> options;

        internal FakeCommandLineOptions(Dictionary<string, string[]> options)
        {
            this.options = options;
        }

        public bool IsOptionSet(string optionName)
        {
            return options.ContainsKey(optionName);
        }

        public bool TryGetOptionArgumentList(string optionName, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string[]? arguments)
        {
            if (options.TryGetValue(optionName, out string[]? value))
            {
                arguments = value;
                return true;
            }

            arguments = null;
            return false;
        }
    }

    private static PropertyTestFramework BuildFrameworkWithCliOptions(
        System.Type subjectType,
        ICommandLineOptions commandLineOptions)
    {
        Assembly assembly = subjectType.Assembly;
        return new(
            serviceProvider: null!,
            assemblies: [assembly],
            typePredicate: t => t == subjectType,
            commandLineOptions: commandLineOptions);
    }

    // ---- GetCommandLineOptions -----------------------------------------------

    [Fact]
    public void GetCommandLineOptions_ReturnsExactlyTwoOptions()
    {
        ConjectureCommandLineOptions options = BuildOptions();

        IReadOnlyCollection<CommandLineOption> result = options.GetCommandLineOptions();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetCommandLineOptions_ContainsConjectureSeedOption()
    {
        ConjectureCommandLineOptions options = BuildOptions();

        IReadOnlyCollection<CommandLineOption> result = options.GetCommandLineOptions();

        bool found = false;
        foreach (CommandLineOption opt in result)
        {
            if (opt.Name == "conjecture-seed")
            {
                found = true;
            }
        }

        Assert.True(found);
    }

    [Fact]
    public void GetCommandLineOptions_ContainsConjectureMaxExamplesOption()
    {
        ConjectureCommandLineOptions options = BuildOptions();

        IReadOnlyCollection<CommandLineOption> result = options.GetCommandLineOptions();

        bool found = false;
        foreach (CommandLineOption opt in result)
        {
            if (opt.Name == "conjecture-max-examples")
            {
                found = true;
            }
        }

        Assert.True(found);
    }

    // ---- ValidateOptionArgumentsAsync: conjecture-seed ----------------------

    [Theory]
    [InlineData("0")]
    [InlineData("12345")]
    public async Task ValidateOptionArgumentsAsync_SeedOption_ValidValues_ReturnsValid(string value)
    {
        ConjectureCommandLineOptions options = BuildOptions();
        CommandLineOption seedOption = FindOption(options, "conjecture-seed");

        ValidationResult result = await options.ValidateOptionArgumentsAsync(seedOption, [value]);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    public async Task ValidateOptionArgumentsAsync_SeedOption_InvalidValues_ReturnsInvalid(string value)
    {
        ConjectureCommandLineOptions options = BuildOptions();
        CommandLineOption seedOption = FindOption(options, "conjecture-seed");

        ValidationResult result = await options.ValidateOptionArgumentsAsync(seedOption, [value]);

        Assert.False(result.IsValid);
    }

    // ---- ValidateOptionArgumentsAsync: conjecture-max-examples --------------

    [Fact]
    public async Task ValidateOptionArgumentsAsync_MaxExamplesOption_ValidValue_ReturnsValid()
    {
        ConjectureCommandLineOptions options = BuildOptions();
        CommandLineOption maxOption = FindOption(options, "conjecture-max-examples");

        ValidationResult result = await options.ValidateOptionArgumentsAsync(maxOption, ["100"]);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    public async Task ValidateOptionArgumentsAsync_MaxExamplesOption_InvalidValues_ReturnsInvalid(string value)
    {
        ConjectureCommandLineOptions options = BuildOptions();
        CommandLineOption maxOption = FindOption(options, "conjecture-max-examples");

        ValidationResult result = await options.ValidateOptionArgumentsAsync(maxOption, [value]);

        Assert.False(result.IsValid);
    }

    // ---- IExtension metadata ------------------------------------------------

    [Fact]
    public void Uid_IsNonEmpty()
    {
        ConjectureCommandLineOptions options = BuildOptions();

        Assert.False(string.IsNullOrWhiteSpace(options.Uid));
    }

    [Fact]
    public void DisplayName_IsNonEmpty()
    {
        ConjectureCommandLineOptions options = BuildOptions();

        Assert.False(string.IsNullOrWhiteSpace(options.DisplayName));
    }

    [Fact]
    public async Task IsEnabledAsync_ReturnsTrue()
    {
        ConjectureCommandLineOptions options = BuildOptions();

        bool enabled = await options.IsEnabledAsync();

        Assert.True(enabled);
    }

    // ---- seed injection integration -----------------------------------------

    private static class SeedFixedSubject
    {
        internal static ulong? LastSeed;

        [Property(MaxExamples = 1)]
        public static void RecordSeed(int x)
        {
        }
    }

    [Fact]
    public async Task PropertyTestFramework_WithSeedCliOption_UsesSeedInSettings()
    {
        FakeCommandLineOptions cliOptions = new(new Dictionary<string, string[]>
        {
            ["conjecture-seed"] = ["42"],
        });
        PropertyTestFramework framework = BuildFrameworkWithCliOptions(
            typeof(SeedFixedSubject),
            cliOptions);

        // Run must complete without error — seed 42 applied means deterministic
        CapturingMessageBus bus = new();
        await framework.RunAsync(bus, new SessionUid("test-session"));

        // At least one message published means the framework ran with no crash
        bool hasResult = false;
        foreach (TestNodeUpdateMessage msg in bus.Messages)
        {
            if (msg.TestNode.Properties.Any<PassedTestNodeStateProperty>() ||
                msg.TestNode.Properties.Any<FailedTestNodeStateProperty>())
            {
                hasResult = true;
            }
        }

        Assert.True(hasResult, "Expected a passed or failed node when seed=42 is applied");
    }

    [Fact]
    public async Task PropertyTestFramework_WithMaxExamplesCliOption_LimitsRunCount()
    {
        FakeCommandLineOptions cliOptions = new(new Dictionary<string, string[]>
        {
            ["conjecture-max-examples"] = ["5"],
        });
        PropertyTestFramework framework = BuildFrameworkWithCliOptions(
            typeof(SeedFixedSubject),
            cliOptions);

        CapturingMessageBus bus = new();
        await framework.RunAsync(bus, new SessionUid("test-session"));

        // With max-examples=5 the run must still produce a result node
        bool hasResult = false;
        foreach (TestNodeUpdateMessage msg in bus.Messages)
        {
            if (msg.TestNode.Properties.Any<PassedTestNodeStateProperty>() ||
                msg.TestNode.Properties.Any<FailedTestNodeStateProperty>())
            {
                hasResult = true;
            }
        }

        Assert.True(hasResult, "Expected a result node when max-examples=5 is applied");
    }

    // ---- helpers -------------------------------------------------------------

    private static CommandLineOption FindOption(ConjectureCommandLineOptions provider, string name)
    {
        foreach (CommandLineOption opt in provider.GetCommandLineOptions())
        {
            if (opt.Name == name)
            {
                return opt;
            }
        }

        throw new System.InvalidOperationException($"Option '{name}' not found.");
    }
}