// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text;

using Aspire.Hosting;

namespace Conjecture.Aspire;

/// <summary>Formats the service-log tail section of a failure report.</summary>
public static class AppLogTailFormatter
{
    /// <summary>Returns a formatted "Service logs" section for the given <paramref name="app"/>, or a placeholder when <paramref name="app"/> is <see langword="null"/>.</summary>
    public static string Format(DistributedApplication? app)
    {
        StringBuilder sb = new();
        sb.AppendLine();
        sb.AppendLine("=== Service logs ===");

        if (app is null)
        {
            sb.AppendLine("(no application available)");
        }

        return sb.ToString();
    }
}
