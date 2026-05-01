// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;
using System.Runtime.CompilerServices;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tests;

internal static class ResourceTestHelpers
{
    internal static RequestContext<T> MakeUninitializedContext<T>()
        where T : class
    {
        Type contextType = typeof(RequestContext<T>);
        object ctx = RuntimeHelpers.GetUninitializedObject(contextType);
        return (RequestContext<T>)ctx;
    }

    internal static RequestContext<ReadResourceRequestParams> MakeReadContext(string uri)
    {
        Type contextType = typeof(RequestContext<ReadResourceRequestParams>);
        object ctx = RuntimeHelpers.GetUninitializedObject(contextType);
        FieldInfo field = contextType.GetField(
            "<Params>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(ctx, new ReadResourceRequestParams { Uri = uri });
        return (RequestContext<ReadResourceRequestParams>)ctx;
    }
}
