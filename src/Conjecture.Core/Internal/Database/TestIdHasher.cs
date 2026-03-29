using System.Security.Cryptography;
using System.Text;

namespace Conjecture.Core.Internal.Database;

internal static class TestIdHasher
{
    internal static string Hash(string fullyQualifiedName)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fullyQualifiedName));
        return Convert.ToHexStringLower(bytes);
    }
}
