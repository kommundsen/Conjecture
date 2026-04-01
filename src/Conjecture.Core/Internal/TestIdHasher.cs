// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Security.Cryptography;
using System.Text;

namespace Conjecture.Core.Internal;

internal static class TestIdHasher
{
    internal static string Hash(string fullyQualifiedName)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fullyQualifiedName));
        return Convert.ToHexStringLower(bytes);
    }
}