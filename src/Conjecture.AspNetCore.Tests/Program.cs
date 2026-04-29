// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Minimal entry point required by WebApplicationFactory<DualEndpointWalkerTestsApp>.
// HostFactoryResolver intercepts host creation during test discovery; this stub
// ensures a valid IHost is built so the factory can configure the test server.

using Microsoft.AspNetCore.Builder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();
await app.RunAsync();