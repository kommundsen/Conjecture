// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Aspire;

internal static class AspirePropertyRunner
{
    internal static async Task RunAsync<TState>(
        IAspireAppFixture fixture,
        AspireStateMachine<TState> machine,
        ConjectureSettings settings,
        CancellationToken cancellationToken)
    {
        DistributedApplication app = await StartWithRetryAsync(fixture, cancellationToken);
        try
        {
            await WaitForHealthChecksAsync(fixture, app, cancellationToken);

            ulong seed = settings.Seed ?? (ulong)Random.Shared.NextInt64();
            SplittableRandom rng = new(seed);

            for (int example = 0; example < settings.MaxExamples; example++)
            {
                if (example > 0)
                {
                    await fixture.ResetAsync(app, cancellationToken);
                    await WaitForHealthChecksAsync(fixture, app, cancellationToken);
                }

                machine.App = app;
                try
                {
                    RunExample(machine, rng);
                }
                finally
                {
                    machine.App = null;
                }
            }
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    private static async Task<DistributedApplication> StartWithRetryAsync(
        IAspireAppFixture fixture,
        CancellationToken cancellationToken)
    {
        int attempts = 0;
        while (true)
        {
            try
            {
                return await fixture.StartAsync(cancellationToken);
            }
            catch (Exception ex) when (IsTransient(ex) && attempts < fixture.MaxRetryAttempts)
            {
                attempts++;
                await Task.Delay(fixture.RetryDelay, cancellationToken);
            }
        }
    }

    private static async Task WaitForHealthChecksAsync(
        IAspireAppFixture fixture,
        DistributedApplication app,
        CancellationToken cancellationToken)
    {
        foreach (string resource in fixture.HealthCheckedResources)
        {
            await fixture.WaitForHealthyAsync(app, resource, cancellationToken);
        }
    }

    private static void RunExample<TState>(AspireStateMachine<TState> machine, SplittableRandom rng)
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        int commandCount = (int)data.NextInteger(1, 20);

        TState state = machine.InitialState();

        for (int i = 0; i < commandCount; i++)
        {
            Strategy<Interaction>[] commands = [.. machine.Commands(state)];
            if (commands.Length == 0)
            {
                break;
            }

            Interaction cmd = new OneOfStrategy<Interaction>(commands).Generate(data);
            state = machine.RunCommand(state, cmd);
            machine.Invariant(state);
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or IOException;
}