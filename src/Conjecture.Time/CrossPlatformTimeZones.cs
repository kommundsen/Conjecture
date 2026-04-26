// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Time;

internal static class CrossPlatformTimeZones
{
    internal static readonly string[] IanaIds =
    [
        "America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles",
        "America/Sao_Paulo", "Europe/London", "Europe/Paris", "Europe/Berlin",
        "Europe/Moscow", "Asia/Tokyo", "Asia/Shanghai", "Asia/Kolkata", "Asia/Dubai",
        "Australia/Sydney", "Pacific/Auckland", "Pacific/Honolulu",
        "Africa/Johannesburg", "UTC",
    ];
}