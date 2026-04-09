// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

namespace Conjecture.Tool.Plan;

public class PlanResult(IDictionary<string, IReadOnlyList<JsonElement>> stepResults)
{
    public IReadOnlyDictionary<string, IReadOnlyList<JsonElement>> StepResults
    {
        get;
    } = new Dictionary<string, IReadOnlyList<JsonElement>>(stepResults);
}