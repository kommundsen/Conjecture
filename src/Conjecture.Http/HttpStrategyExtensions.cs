// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

using Conjecture.Core;

namespace Conjecture.Http;

/// <summary>Extension methods on <see cref="Strategy"/> for HTTP interaction generation.</summary>
public static class HttpStrategyExtensions
{
    extension(Strategy)
    {
        /// <summary>Returns a fluent <see cref="HttpStrategyBuilder"/> bound to <paramref name="resourceName"/>.</summary>
        public static HttpStrategyBuilder Http(string resourceName)
        {
            ArgumentNullException.ThrowIfNull(resourceName);
            return new HttpStrategyBuilder(resourceName);
        }
    }
}