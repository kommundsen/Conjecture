// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Net.Http;

using Conjecture.Core;

using Microsoft.Extensions.Hosting;

namespace Conjecture.AspNetCore;

/// <summary>Extension methods on <see cref="Generate"/> for ASP.NET Core endpoint discovery.</summary>
public static class AspNetCoreExtensions
{
    extension(Generate)
    {
        /// <summary>Returns a fluent <see cref="AspNetCoreRequestBuilder"/> bound to <paramref name="host"/> and <paramref name="client"/>.</summary>
        public static AspNetCoreRequestBuilder AspNetCoreRequests(IHost host, HttpClient client)
        {
            ArgumentNullException.ThrowIfNull(host);
            ArgumentNullException.ThrowIfNull(client);
            return new AspNetCoreRequestBuilder(host, client);
        }
    }
}