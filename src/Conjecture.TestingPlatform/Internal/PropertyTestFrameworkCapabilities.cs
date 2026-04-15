// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Conjecture.TestingPlatform.Internal;

internal sealed class PropertyTestFrameworkCapabilities : ITestFrameworkCapabilities, ITrxReportCapability
{
    public bool IsSupported => true;

    public bool TrxEnabled { get; private set; }

    public void Enable()
    {
        TrxEnabled = true;
    }

    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => [this];
}