// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Conjecture.Core.Internal;

internal static class ValueRenderer
{
    private static readonly Dictionary<Type, string> TypeAliases = new()
    {
        [typeof(int)] = "int",
        [typeof(bool)] = "bool",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(string)] = "string",
        [typeof(byte[])] = "byte[]",
        [typeof(long)] = "long",
        [typeof(short)] = "short",
        [typeof(uint)] = "uint",
        [typeof(ulong)] = "ulong",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(object)] = "object",
    };

    private static string GetTypeName(Type type)
    {
        if (TypeAliases.TryGetValue(type, out string? alias))
        {
            return alias;
        }

        if (type.IsArray)
        {
            return $"{GetTypeName(type.GetElementType()!)}[]";
        }

        Type? nullableUnderlying = Nullable.GetUnderlyingType(type);
        if (nullableUnderlying is not null)
        {
            return $"{GetTypeName(nullableUnderlying)}?";
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        string baseName = type.Name[..type.Name.IndexOf('`')];
        string args = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
        return $"{baseName}<{args}>";
    }

    [RequiresUnreferencedCode("JSON serialization of arbitrary types requires type metadata at runtime.")]
    internal static string RenderLiteral(string paramName, object? value, Type type)
    {
        string typeName = GetTypeName(type);

        if (value is null)
        {
            return $"var {paramName} = ({typeName})null!;";
        }

        Func<object, string>? formatter = FormatterRegistry.GetUntyped(type);
        if (formatter is not null)
        {
            string formatted = formatter(value);
            return $"var {paramName} = {formatted};";
        }

        try
        {
            string json = JsonSerializer.Serialize(value, type);
            return $"var {paramName} = JsonSerializer.Deserialize<{typeName}>(\"\"\"{json}\"\"\");";
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            string line1 = $"// WARNING: {type.Name} cannot be serialized.";
            string line2 = $"// Value was: {value}";
            string line3 = $"var {paramName} = default({typeName})!;";
            return $"{line1}\n{line2}\n{line3}";
        }
    }
}