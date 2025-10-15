// ReSharper disable CheckNamespace
// ReSharper disable UnusedType.Global

#if !NET5_0_OR_GREATER

using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill for init-only properties in netstandard2.0.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit
{
}

#endif
