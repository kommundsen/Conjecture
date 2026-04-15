// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.TestingPlatform.Internal;

using Microsoft.Testing.Platform.Builder;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.CommandLine.AddProvider(static () => new ConjectureCommandLineOptions());
builder.RegisterTestFramework(
    _ => new PropertyTestFrameworkCapabilities(),
    (_, services) => new PropertyTestFramework(services));
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();