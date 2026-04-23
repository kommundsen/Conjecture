// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Interactions;

namespace Conjecture.Http;

/// <summary>
/// An <see cref="IInteractionTarget"/> that dispatches <see cref="HttpInteraction"/>s
/// using an <see cref="HttpClient"/> resolved per resource name.
/// </summary>
public interface IHttpTarget : IInteractionTarget
{
    /// <summary>Resolves the <see cref="HttpClient"/> to use for the given <paramref name="resourceName"/>.</summary>
    HttpClient ResolveClient(string resourceName);

    /// <inheritdoc/>
    async Task<object?> IInteractionTarget.ExecuteAsync(IInteraction interaction, CancellationToken ct)
    {
        if (interaction is not HttpInteraction http)
        {
            throw new ArgumentException(
                $"{nameof(IHttpTarget)} can only execute {nameof(HttpInteraction)}; got {interaction.GetType().Name}.",
                nameof(interaction));
        }

        HttpClient client = ResolveClient(http.ResourceName);
        using HttpRequestMessage request = new(new HttpMethod(http.Method), http.Path);
        if (http.Body is HttpContent content)
        {
            request.Content = content;
        }

        if (http.Headers is not null)
        {
            foreach (KeyValuePair<string, string> header in http.Headers)
            {
                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        HttpResponseMessage response = await client.SendAsync(request, ct).ConfigureAwait(false);
        return response;
    }
}