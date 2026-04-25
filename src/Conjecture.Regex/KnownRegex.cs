// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.RegularExpressions;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Regex;

/// <summary>Curated source-generated <see cref="DotNetRegex"/> instances for common patterns.</summary>
public static partial class KnownRegex
{
    /// <summary>Matches a simple email address (local@domain.tld).</summary>
    [GeneratedRegex(@"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$")]
    public static partial DotNetRegex Email { get; }

    /// <summary>Matches an HTTP or HTTPS URL.</summary>
    [GeneratedRegex(@"^https?://[a-zA-Z0-9.\-]+(:[0-9]{1,5})?(/[a-zA-Z0-9._~:/?#\[\]@!$&'()*+,;%=\-]*)?$")]
    public static partial DotNetRegex Url { get; }

    /// <summary>Matches a UUID in the canonical 8-4-4-4-12 hyphenated format.</summary>
    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    public static partial DotNetRegex Uuid { get; }

    /// <summary>Matches an ISO 8601 calendar date (YYYY-MM-DD).</summary>
    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$")]
    public static partial DotNetRegex IsoDate { get; }

    /// <summary>Matches a 16-digit credit card number in 4-4-4-4 format with a consistent separator (space, hyphen, or none).</summary>
    [GeneratedRegex(@"^\d{4}([ \-]?)\d{4}\1\d{4}\1\d{4}$")]
    public static partial DotNetRegex CreditCard { get; }

    /// <summary>Matches an IPv4 address in dotted-quad notation (0–255 in each octet).</summary>
    [GeneratedRegex(@"^(25[0-5]|2[0-4]\d|1\d{2}|[1-9]\d|\d)\.(25[0-5]|2[0-4]\d|1\d{2}|[1-9]\d|\d)\.(25[0-5]|2[0-4]\d|1\d{2}|[1-9]\d|\d)\.(25[0-5]|2[0-4]\d|1\d{2}|[1-9]\d|\d)$")]
    public static partial DotNetRegex Ipv4 { get; }

    /// <summary>Matches an IPv6 address in full or compressed form.</summary>
    [GeneratedRegex(@"^(([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}|(([0-9a-fA-F]{1,4}:)*[0-9a-fA-F]{1,4})?::(([0-9a-fA-F]{1,4}:)*[0-9a-fA-F]{1,4})?)$")]
    public static partial DotNetRegex Ipv6 { get; }

    /// <summary>Matches an RFC 3339 full-date string (YYYY-MM-DD).</summary>
    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$")]
    public static partial DotNetRegex Date { get; }

    /// <summary>Matches an RFC 3339 partial-time string (HH:MM:SS).</summary>
    [GeneratedRegex(@"^([01]\d|2[0-3]):[0-5]\d:[0-5]\d$")]
    public static partial DotNetRegex Time { get; }
}
