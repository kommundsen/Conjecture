// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Runtime.CompilerServices;

using Conjecture.Core;

namespace Conjecture.AspNetCore.Tests.TestSupport;

/// <summary>
/// Registers <see cref="CreateOrderRequest"/> with <see cref="GenForRegistry"/> so
/// <c>RequestSynthesizer</c> can synthesize bodies for it via <see cref="GenForRegistry.ResolveBoxed"/>.
/// </summary>
internal static class CreateOrderRequestStrategySupport
{
    [ModuleInitializer]
    internal static void Register()
    {
        GenForRegistry.Register(
            typeof(CreateOrderRequest),
            static () => new CreateOrderRequestProvider(),
            Generate.Compose<object?>(static ctx =>
            {
                string productId = ctx.Generate(Generate.Strings(minLength: 1, maxLength: 10));
                int quantity = ctx.Generate(Generate.Integers<int>(1, 100));
                return (object?)new CreateOrderRequest(productId, quantity);
            }));
    }

    private sealed class CreateOrderRequestProvider : IStrategyProvider<CreateOrderRequest>
    {
        public Strategy<CreateOrderRequest> Create() =>
            Generate.Compose<CreateOrderRequest>(static ctx =>
            {
                string productId = ctx.Generate(Generate.Strings(minLength: 1, maxLength: 10));
                int quantity = ctx.Generate(Generate.Integers<int>(1, 100));
                return new CreateOrderRequest(productId, quantity);
            });
    }
}
