// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Conjecture.AspNetCore;

internal sealed record EndpointParameter(
    string Name,
    Type ClrType,
    BindingSource Source,
    bool IsRequired);