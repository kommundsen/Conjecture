// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Aspire.Http;
using Conjecture.Http;

namespace Conjecture.Aspire.Http.Tests;

public sealed class DistributedApplicationHttpTargetTests
{
    [Fact]
    public void DistributedApplicationHttpTarget_ImplementsIHttpTarget()
    {
        Assert.True(typeof(DistributedApplicationHttpTarget).IsAssignableTo(typeof(IHttpTarget)));
    }
}
