// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class StateMachineStrategy<TMachine, TState, TCommand>(int maxSteps = 50)
    : Strategy<StateMachineRun<TState>>, IStrategyProvider<StateMachineRun<TState>>
    where TMachine : IStateMachine<TState, TCommand>, new()
{
    public Strategy<StateMachineRun<TState>> Create() => this;

    internal override StateMachineRun<TState> Generate(ConjectureData data)
    {
        TMachine machine = new();
        TState initialState = machine.InitialState();
        TState state = initialState;
        int length = (int)data.NextInteger(0, (ulong)maxSteps);
        List<ExecutedStep<TState>> steps = new(length);
        int? failureStep = null;
        IStrategyFormatter<TCommand>? formatter = FormatterRegistry.Get<TCommand>();

        for (int i = 0; i < length; i++)
        {
            Strategy<TCommand>[] commands = [..machine.Commands(state)];
            if (commands.Length == 0)
            {
                break;
            }

            data.InsertCommandStart();
            TCommand command = new OneOfStrategy<TCommand>(commands).Generate(data);
            state = machine.RunCommand(state, command);
            string label = formatter?.Format(command!) ?? command?.ToString() ?? string.Empty;

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
