// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text;

using Grpc.Core;

namespace Conjecture.Grpc;

/// <summary>Fluent invariant assertions for <see cref="GrpcResponse"/> values.</summary>
public static class GrpcInvariantExtensions
{
    /// <summary>Asserts the response status is <see cref="StatusCode.OK"/>.</summary>
    public static GrpcResponse AssertStatusOk(this GrpcResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Status == StatusCode.OK
            ? response
            : throw new GrpcInvariantException(BuildMessage(response, StatusCode.OK));
    }

    /// <summary>Asserts the response status matches <paramref name="expected"/>.</summary>
    public static GrpcResponse AssertStatus(this GrpcResponse response, StatusCode expected)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Status == expected
            ? response
            : throw new GrpcInvariantException(BuildMessage(response, expected));
    }

    /// <summary>Asserts the response status is not <see cref="StatusCode.Unknown"/>.</summary>
    public static GrpcResponse AssertNoUnknownStatus(this GrpcResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Status != StatusCode.Unknown
            ? response
            : throw new GrpcInvariantException(BuildMessage(response, null));
    }

    private static string BuildMessage(GrpcResponse response, StatusCode? expected)
    {
        StringBuilder sb = new();

        if (expected is StatusCode exp)
        {
            sb.Append($"Expected status {exp} but got {response.Status}.");
        }
        else
        {
            sb.Append($"Expected non-Unknown status but got {response.Status}.");
        }

        if (!string.IsNullOrEmpty(response.StatusDetail))
        {
            sb.Append($" Detail: {response.StatusDetail}.");
        }

        if (response.Trailers.Count > 0)
        {
            sb.Append(" Trailers: {");
            bool first = true;
            foreach (KeyValuePair<string, string> trailer in response.Trailers)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                sb.Append($"{trailer.Key}={trailer.Value}");
                first = false;
            }

            sb.Append('}');
        }

        return sb.ToString();
    }
}