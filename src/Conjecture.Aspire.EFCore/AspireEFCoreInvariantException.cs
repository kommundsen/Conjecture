// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

using Conjecture.EFCore;

namespace Conjecture.Aspire.EFCore;

/// <summary>Thrown when an Aspire + EF Core invariant assertion fails.</summary>
public sealed class AspireEFCoreInvariantException : DbInvariantException
{
    /// <summary>Initializes a new instance with <paramref name="message"/>.</summary>
    public AspireEFCoreInvariantException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with <paramref name="message"/> and <paramref name="innerException"/>.</summary>
    public AspireEFCoreInvariantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
