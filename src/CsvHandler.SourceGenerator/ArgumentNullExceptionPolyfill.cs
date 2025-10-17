// Copyright (c) 2025 CsvHandler Contributors
// Licensed under the MIT License

// Polyfill for ArgumentNullException.ThrowIfNull for netstandard2.0
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates the name of the parameter that provides the argument.
    /// Used for parameter validation messages.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }
}

namespace System
{
    internal static class ArgumentNullExceptionPolyfill
    {
        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.
        /// </summary>
        /// <param name="argument">The reference type argument to validate as non-null.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        public static void ThrowIfNull(
            object? argument,
            [System.Runtime.CompilerServices.CallerArgumentExpression("argument")] string? paramName = null)
        {
            if (argument is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }
}
