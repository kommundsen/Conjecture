// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace LINQPad;

/// <summary>Stub for tests — mirrors the real LINQPad.ICustomMemberProvider interface.</summary>
public interface ICustomMemberProvider
{
    IEnumerable<string> GetNames();
    IEnumerable<System.Type> GetTypes();
    IEnumerable<object?> GetValues();
}

/// <summary>Stub for tests — mirrors LINQPad.Util.</summary>
public static class Util
{
    public static object RawHtml(string html)
    {
        return new RawHtml(html);
    }
}

/// <summary>Stub for tests — mirrors LINQPad.RawHtml.</summary>
public sealed class RawHtml(string html)
{
    public override string ToString()
    {
        return html;
    }
}