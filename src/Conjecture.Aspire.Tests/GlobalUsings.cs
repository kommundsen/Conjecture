// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Compatibility shim: DistributedApplication.CreateBuilder returns IDistributedApplicationBuilder;
// alias DistributedApplicationBuilder → IDistributedApplicationBuilder so test helpers compile.
global using DistributedApplicationBuilder = Aspire.Hosting.IDistributedApplicationBuilder;