// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Conjecture.TestingPlatform.Internal;

internal sealed class PropertyTestFrameworkCapabilities : ITestFrameworkCapabilities
{
    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => [];
}