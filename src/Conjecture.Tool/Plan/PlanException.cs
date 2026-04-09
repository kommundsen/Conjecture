// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Tool.Plan;

public class PlanException(string message, int exitCode = 1) : Exception(message)
{
    public int ExitCode
    {
        get;
    } = exitCode;
}