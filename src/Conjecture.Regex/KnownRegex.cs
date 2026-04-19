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
}