// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Net.Http;

using Aspire.Hosting;
using Aspire.Hosting.Testing;

using Conjecture.Http;

namespace Conjecture.Aspire.Http;

/// <summary>An <see cref="IHttpTarget"/> that resolves HTTP clients from a <see cref="DistributedApplication"/>.</summary>
public sealed class DistributedApplicationHttpTarget(DistributedApplication app) : IHttpTarget
{
    /// <inheritdoc/>
    public HttpClient ResolveClient(string resourceName) => app.CreateHttpClient(resourceName);
}
