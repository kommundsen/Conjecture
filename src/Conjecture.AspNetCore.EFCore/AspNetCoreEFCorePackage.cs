// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Conjecture.AspNetCore.EFCore;

internal static class AspNetCoreEFCorePackage
{
    internal static readonly System.Type WebApplicationFactory = typeof(WebApplicationFactory<>);
    internal static readonly System.Type DbContext = typeof(DbContext);
}