// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.RegularExpressions;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Regex;

/// <summary>Curated compiled <see cref="DotNetRegex"/> constants for common patterns.</summary>
public static class KnownRegex
{
    /// <summary>Matches a simple email address (local@domain.tld).</summary>
    public static readonly DotNetRegex Email =
        new(@"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);

    /// <summary>Matches an HTTP or HTTPS URL.</summary>
    public static readonly DotNetRegex Url =
        new(@"^https?://[a-zA-Z0-9.\-]+(:[0-9]{1,5})?(/[a-zA-Z0-9._~:/?#\[\]@!$&'()*+,;%=\-]*)?$", RegexOptions.Compiled);

    /// <summary>Matches a UUID in the canonical 8-4-4-4-12 hyphenated format.</summary>
    public static readonly DotNetRegex Uuid =
        new(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled);

    /// <summary>Matches an ISO 8601 calendar date (YYYY-MM-DD).</summary>
    public static readonly DotNetRegex IsoDate =
        new(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$", RegexOptions.Compiled);

    /// <summary>Matches a 16-digit credit card number in 4-4-4-4 format with a consistent separator (space, hyphen, or none).</summary>
    public static readonly DotNetRegex CreditCard =
        new(@"^\d{4}([ \-]?)\d{4}\1\d{4}\1\d{4}$", RegexOptions.Compiled);

    /// <summary>Matches an IPv4 address in dotted-quad notation (0–255 in each octet).</summary>
    public static readonly DotNetRegex Ipv4 =
        new(@"^(25[0-5]|2[0-4]\d|1\d{2}|[1-9]\d|\d)\.(25[0-5]|2[0-4]\d|1\d{2}|[1-9]\d|\d)\.(25[0-5]|2[0-4]\d|1\d{2}|[1-9]\d|\d)\.(25[0-5]|2[0-4]\d|1\d{2}|[1-9]\d|\d)$", RegexOptions.Compiled);

    /// <summary>Matches an IPv6 address in full or compressed form.</summary>
    public static readonly DotNetRegex Ipv6 =
        new(@"^(([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}|(([0-9a-fA-F]{1,4}:)*[0-9a-fA-F]{1,4})?::(([0-9a-fA-F]{1,4}:)*[0-9a-fA-F]{1,4})?)$", RegexOptions.Compiled);

    /// <summary>Matches an RFC 3339 full-date string (YYYY-MM-DD).</summary>
    public static readonly DotNetRegex Date =
        new(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$", RegexOptions.Compiled);

    /// <summary>Matches an RFC 3339 partial-time string (HH:MM:SS).</summary>
    public static readonly DotNetRegex Time =
        new(@"^([01]\d|2[0-3]):[0-5]\d:[0-5]\d$", RegexOptions.Compiled);
}