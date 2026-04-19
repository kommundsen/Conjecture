// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.TestingPlatform;

using Microsoft.Testing.Platform.Builder;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterConjectureFramework();
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
