// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Conjecture.AspNetCore;

/// <summary>Describes a single parameter on a discovered endpoint.</summary>
/// <param name="Name">The route or query segment key used to populate the parameter.</param>
/// <param name="ClrType">The runtime type used to select a generation strategy.</param>
/// <param name="Source">Where the parameter is bound from (path, query, body, etc.).</param>
/// <param name="IsRequired">Whether the parameter must be present in the request.</param>
public sealed record EndpointParameter(
    string Name,
    Type ClrType,
    BindingSource Source,
    bool IsRequired);