// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class VersionStrategy : Strategy<Version>
{
    private readonly IntegerStrategy<int> majorStrategy;
    private readonly IntegerStrategy<int> minorStrategy;
    private readonly IntegerStrategy<int> buildStrategy;
    private readonly IntegerStrategy<int> revisionStrategy;

    internal VersionStrategy(int maxMajor, int maxMinor, int maxBuild, int maxRevision)
    {
        majorStrategy = new IntegerStrategy<int>(0, maxMajor);
        minorStrategy = new IntegerStrategy<int>(0, maxMinor);
        buildStrategy = new IntegerStrategy<int>(0, maxBuild);
        revisionStrategy = new IntegerStrategy<int>(0, maxRevision);
    }

    internal override Version Generate(ConjectureData data)
    {
        int major = majorStrategy.Generate(data);
        int minor = minorStrategy.Generate(data);
        int build = buildStrategy.Generate(data);
        int revision = revisionStrategy.Generate(data);
        return new(major, minor, build, revision);
    }
}