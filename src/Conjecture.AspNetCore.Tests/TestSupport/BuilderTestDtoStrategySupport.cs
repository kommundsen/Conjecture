// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Runtime.CompilerServices;

using Conjecture.Core;

namespace Conjecture.AspNetCore.Tests.TestSupport;

/// <summary>
/// Registers <see cref="BuilderTestDto"/> with <see cref="GenerateForRegistry"/> so
/// <c>RequestSynthesizer</c> can synthesize bodies for it via <see cref="GenerateForRegistry.ResolveBoxed"/>.
/// </summary>
internal static class BuilderTestDtoStrategySupport
{
    [ModuleInitializer]
    internal static void Register()
    {
        GenerateForRegistry.Register(
            typeof(BuilderTestDto),
            static () => new BuilderTestDtoProvider(),
            Strategy.Compose<object?>(static ctx =>
            {
                string name = ctx.Generate(Strategy.Strings(minLength: 1, maxLength: 20));
                return (object?)new BuilderTestDto(name);
            }));
    }

    private sealed class BuilderTestDtoProvider : IStrategyProvider<BuilderTestDto>
    {
        public Strategy<BuilderTestDto> Create() =>
            Strategy.Compose<BuilderTestDto>(static ctx =>
            {
                string name = ctx.Generate(Strategy.Strings(minLength: 1, maxLength: 20));
                return new BuilderTestDto(name);
            });
    }
}