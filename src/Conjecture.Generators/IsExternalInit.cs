// Polyfill required to use record types and init-only setters when targeting netstandard2.0.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Runtime.CompilerServices;
#pragma warning restore IDE0130

internal static class IsExternalInit { }
