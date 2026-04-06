// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Collections.Generic;
using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class StateMachineStrategy<TMachine, TState, TCommand>(int maxSteps = 50)
    : Strategy<StateMachineRun<TState>>, IStrategyProvider<StateMachineRun<TState>>
    where TMachine : IStateMachine<TState, TCommand>, new()
{
    static StateMachineStrategy()
    {
        FormatterRegistry.Register<StateMachineRun<TState>>(new StateMachineFormatter<TState>());
    }

    public Strategy<StateMachineRun<TState>> Create() => this;

    internal override StateMachineRun<TState> Generate(ConjectureData data)
    {
        TMachine machine = new();
        TState state = machine.InitialState();
        int length = (int)data.NextInteger(0, (ulong)maxSteps);
        List<TCommand> drawn = new(length);

        for (int i = 0; i < length; i++)
        {
            Strategy<TCommand>[] commands = [..machine.Commands(state)];
            if (commands.Length == 0)
            {
                break;
            }

            data.InsertCommandStart();
            TCommand command = new OneOfStrategy<TCommand>(commands).Generate(data);
            drawn.Add(command);
            state = machine.RunCommand(state, command);
        }

        (StateMachineRun<TState> run, Exception? invariantException) = StateMachineRunner.Execute(machine, drawn);
        if (!run.Passed)
        {
            string message = StateMachineRunner.FormatSteps(run.Steps, run.FailureStepIndex);
            throw new Exception(message, invariantException);
        }
        return run;
    }
}
