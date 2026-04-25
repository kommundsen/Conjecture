// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Interactions;

namespace Conjecture.Http;

/// <summary>An HTTP call addressed to a named resource.</summary>
/// <param name="ResourceName">Logical name of the target resource (used to resolve an <see cref="System.Net.Http.HttpClient"/>).</param>
/// <param name="Method">HTTP method (e.g. <c>GET</c>, <c>POST</c>).</param>
/// <param name="Path">Request path or absolute URI.</param>
/// <param name="Body">Optional request body. May be an <see cref="System.Net.Http.HttpContent"/> or a value to be serialized by the target.</param>
/// <param name="Headers">Optional request headers to apply.</param>
public readonly record struct HttpInteraction(
    string ResourceName,
    string Method,
    string Path,
    object? Body,
    IReadOnlyDictionary<string, string>? Headers) : IAddressedInteraction;