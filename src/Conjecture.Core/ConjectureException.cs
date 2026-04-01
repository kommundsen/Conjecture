// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>Thrown when a Conjecture health check fails (e.g., too many unsatisfied assumptions).</summary>
public sealed class ConjectureException(string message) : Exception(message);