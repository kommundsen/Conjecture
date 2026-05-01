// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Collections.Generic;
using System.Text;

using Conjecture.Abstractions.Strategies;

namespace Conjecture.Core.Internal;

internal static class StateMachineRunner
{
    internal static (StateMachineRun<TState> Run, Exception? InvariantException) Execute<TState, TCommand>(
        IStateMachine<TState, TCommand> machine,
        IReadOnlyList<TCommand> commands)
    {
        TState initialState = machine.InitialState();
        TState state = initialState;
        IStrategyFormatter<TCommand>? formatter = FormatterRegistry.Get<TCommand>();
        List<ExecutedStep<TState>> steps = new(commands.Count);
        int? failureStep = null;
        Exception? invariantException = null;

        for (int i = 0; i < commands.Count; i++)
        {
            state = machine.RunCommand(state, commands[i]);
            string label = formatter?.Format(commands[i]!) ?? commands[i]?.ToString() ?? string.Empty;

            try
            {
                machine.Invariant(state);
            }
            catch (Exception ex)
            {
                // IStateMachine.Invariant signals a violation by throwing any exception type
                failureStep = i;
                invariantException = ex;
                steps.Add(new ExecutedStep<TState>(state, label));
                break;
            }

            steps.Add(new ExecutedStep<TState>(state, label));
        }

        return (new StateMachineRun<TState>(steps, initialState, failureStep), invariantException);
    }

    internal static string FormatSteps<TState>(IReadOnlyList<ExecutedStep<TState>> steps, int? failureStepIndex)
    {
        StringBuilder sb = new();
        sb.AppendLine($"State machine invariant violated at step {failureStepIndex}.");
        sb.AppendLine("Executed steps:");
        for (int i = 0; i < steps.Count; i++)
        {
            string marker = i == failureStepIndex ? " ✗" : "  ";
            sb.AppendLine($"{marker}[{i}] {steps[i].CommandLabel} → state={steps[i].State}");
        }
        return sb.ToString().TrimEnd();
    }
}