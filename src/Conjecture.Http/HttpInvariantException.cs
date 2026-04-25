// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

namespace Conjecture.Http;

/// <summary>Thrown when an HTTP response invariant assertion fails.</summary>
public sealed class HttpInvariantException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="HttpInvariantException"/> class.</summary>
    public HttpInvariantException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HttpInvariantException"/> class.</summary>
    public HttpInvariantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}