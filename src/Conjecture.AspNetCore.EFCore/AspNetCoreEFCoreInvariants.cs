// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.EFCore;
using Conjecture.Http;

namespace Conjecture.AspNetCore.EFCore;

/// <summary>Combines an <see cref="IHttpTarget"/> and an <see cref="IDbTarget"/> to assert HTTP/EF Core invariants.</summary>
public sealed class AspNetCoreEFCoreInvariants(IHttpTarget http, IDbTarget db)
{
    private readonly IHttpTarget http = http ?? throw new ArgumentNullException(nameof(http));
    private readonly IDbTarget db = db ?? throw new ArgumentNullException(nameof(db));

    /// <summary>
    /// Asserts that a failing HTTP request (status &gt;= 400) did not persist any entity changes.
    /// </summary>
    public async Task AssertNoPartialWritesOnErrorAsync(
        Func<HttpClient, CancellationToken, Task<HttpResponseMessage>> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        EntitySnapshot before = await EntitySnapshotter.CaptureAsync(db, cancellationToken).ConfigureAwait(false);

        HttpClient client = http.ResolveClient(db.ResourceName);
        HttpResponseMessage response = await request(client, cancellationToken).ConfigureAwait(false);

        EntitySnapshot after = await EntitySnapshotter.CaptureAsync(db, cancellationToken).ConfigureAwait(false);

        if ((int)response.StatusCode < 400)
        {
            return;
        }

        EntitySnapshotDiff diff = EntitySnapshotter.Diff(before, after);
        if (diff.IsEmpty)
        {
            return;
        }

        string method = response.RequestMessage?.Method.Method ?? "<unknown>";
        string path = response.RequestMessage?.RequestUri?.PathAndQuery ?? "<unknown>";
        int status = (int)response.StatusCode;
        throw new AspNetCoreEFCoreInvariantException(
            FormattableString.Invariant(
                $"AssertNoPartialWritesOnError: {method} {path} returned {status} but persisted changes: {diff.ToReport()}"));
    }
}