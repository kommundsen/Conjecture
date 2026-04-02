// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.Core.Internal;

internal static class StateMachineRunner
{
    internal static StateMachineRun<TState> Execute<TState, TCommand>(
        IStateMachine<TState, TCommand> machine,
        IReadOnlyList<TCommand> commands)
    {
        TState initialState = machine.InitialState();
        TState state = initialState;
        IStrategyFormatter<TCommand>? formatter = FormatterRegistry.Get<TCommand>();
        List<ExecutedStep<TState>> steps = new(commands.Count);
        int? failureStep = null;

        for (int i = 0; i < commands.Count; i++)
        {
            state = machine.RunCommand(state, commands[i]);
            string label = formatter?.Format(commands[i]!) ?? commands[i]?.ToString() ?? string.Empty;

            bool invariantFailed = false;
            try
            {
                machine.Invariant(state);
            }
            catch
            {
                // IStateMachine.Invariant signals a violation by throwing any exception type
                failureStep = i;
                invariantFailed = true;
            }

            steps.Add(new ExecutedStep<TState>(state, label));
            if (invariantFailed)
            {
                break;
            }
        }

        return new StateMachineRun<TState>(steps, initialState, failureStep);
    }
}
