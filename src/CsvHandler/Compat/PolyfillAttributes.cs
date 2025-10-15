// Copyright (c) 2025 CsvHandler Contributors
// Licensed under the MIT License

// Polyfill attributes for AOT/trim compatibility and nullable annotations on netstandard2.0

#if NETSTANDARD2_0

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Specifies that null is allowed as an input even if the corresponding type disallows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property,
                    Inherited = false)]
    internal sealed class AllowNullAttribute : Attribute { }

    /// <summary>
    /// Specifies that null is disallowed as an input even if the corresponding type allows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property,
                    Inherited = false)]
    internal sealed class DisallowNullAttribute : Attribute { }

    /// <summary>
    /// Specifies that an output may be null even if the corresponding type disallows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property |
                    AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class MaybeNullAttribute : Attribute { }

    /// <summary>
    /// Specifies that an output will not be null even if the corresponding type allows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property |
                    AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class NotNullAttribute : Attribute { }

    /// <summary>
    /// Specifies that when a method returns <see cref="ReturnValue"/>,
    /// the parameter will not be null even if the corresponding type allows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue)
        {
            ReturnValue = returnValue;
        }

        public bool ReturnValue { get; }
    }

    /// <summary>
    /// Specifies that when a method returns <see cref="ReturnValue"/>,
    /// the parameter may be null even if the corresponding type disallows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class MaybeNullWhenAttribute : Attribute
    {
        public MaybeNullWhenAttribute(bool returnValue)
        {
            ReturnValue = returnValue;
        }

        public bool ReturnValue { get; }
    }

    /// <summary>
    /// Specifies that the method or property will ensure that the listed field and property members have not-null values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property,
                    Inherited = false, AllowMultiple = true)]
    internal sealed class MemberNotNullAttribute : Attribute
    {
        public MemberNotNullAttribute(string member)
        {
            Members = new[] { member };
        }

        public MemberNotNullAttribute(params string[] members)
        {
            Members = members;
        }

        public string[] Members { get; }
    }

    /// <summary>
    /// Specifies that the method or property will ensure that the listed field and property members have not-null values
    /// when returning with the specified return value condition.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property,
                    Inherited = false, AllowMultiple = true)]
    internal sealed class MemberNotNullWhenAttribute : Attribute
    {
        public MemberNotNullWhenAttribute(bool returnValue, string member)
        {
            ReturnValue = returnValue;
            Members = new[] { member };
        }

        public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
        {
            ReturnValue = returnValue;
            Members = members;
        }

        public bool ReturnValue { get; }
        public string[] Members { get; }
    }

    /// <summary>
    /// Indicates that the specified method requires dynamic code generation.
    /// This code may not work when the application is deployed as native AOT.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class,
                    Inherited = false, AllowMultiple = false)]
    internal sealed class RequiresDynamicCodeAttribute : Attribute
    {
        public RequiresDynamicCodeAttribute(string message)
        {
            Message = message;
        }

        public string Message { get; }
        public string? Url { get; set; }
    }

    /// <summary>
    /// Indicates that the specified method requires the ability to use unreferenced code.
    /// This code may be trimmed away when publishing with trimming enabled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class,
                    Inherited = false, AllowMultiple = false)]
    internal sealed class RequiresUnreferencedCodeAttribute : Attribute
    {
        public RequiresUnreferencedCodeAttribute(string message)
        {
            Message = message;
        }

        public string Message { get; }
        public string? Url { get; set; }
    }

    /// <summary>
    /// Indicates which members are preserved by the framework when trimming.
    /// </summary>
    [Flags]
    internal enum DynamicallyAccessedMemberTypes
    {
        None = 0,
        PublicConstructors = 1,
        NonPublicConstructors = 2,
        PublicMethods = 4,
        NonPublicMethods = 8,
        PublicFields = 16,
        NonPublicFields = 32,
        PublicNestedTypes = 64,
        NonPublicNestedTypes = 128,
        PublicProperties = 256,
        NonPublicProperties = 512,
        PublicEvents = 1024,
        NonPublicEvents = 2048,
        Interfaces = 4096,
        All = -1
    }

    /// <summary>
    /// Indicates that certain members on a specified <see cref="Type"/> are accessed dynamically,
    /// for example through reflection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method |
                    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Interface |
                    AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter,
                    Inherited = false)]
    internal sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
        {
            MemberTypes = memberTypes;
        }

        public DynamicallyAccessedMemberTypes MemberTypes { get; }
    }

    /// <summary>
    /// Suppresses reporting of a specific rule violation, allowing multiple suppressions on a single code artifact.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        public UnconditionalSuppressMessageAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }

        public string Category { get; }
        public string CheckId { get; }
        public string? Scope { get; set; }
        public string? Target { get; set; }
        public string? MessageId { get; set; }
        public string? Justification { get; set; }
    }
}

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

    /// <summary>
    /// Reserved to be used by the compiler for tracking metadata.
    /// This class should not be used by developers in source code.
    /// Used for init-only properties (C# 9.0).
    /// </summary>
    internal static class IsExternalInit { }

    /// <summary>
    /// Indicates that a method should skip initializing local variables to zero.
    /// Can improve performance but requires careful usage.
    /// </summary>
    [AttributeUsage(AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct |
                    AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property |
                    AttributeTargets.Event, Inherited = false)]
    internal sealed class SkipLocalsInitAttribute : Attribute { }

    /// <summary>
    /// Indicates that a member is required when initializing an object.
    /// Used for required members (C# 11.0).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property |
                    AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute { }

    /// <summary>
    /// Indicates that compiler features are required for a particular element.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        public string FeatureName { get; }
        public bool IsOptional { get; init; }
    }
}

namespace System.Diagnostics
{
    /// <summary>
    /// Indicates that a constructor sets all required members of the containing type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }
}

#endif

// Polyfill for Range type (netstandard2.0)
#if NETSTANDARD2_0
namespace System
{
    /// <summary>
    /// Represents a range that has start and end indexes.
    /// </summary>
    public readonly struct Range
    {
        public Range(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }
        public int End { get; }
    }
}
#endif

// Polyfill for RequiresDynamicCodeAttribute (net6.0)
#if NET6_0
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Indicates that the specified method requires dynamic code generation.
    /// This code may not work when the application is deployed as native AOT.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class,
                    Inherited = false, AllowMultiple = false)]
    internal sealed class RequiresDynamicCodeAttribute : Attribute
    {
        public RequiresDynamicCodeAttribute(string message)
        {
            Message = message;
        }

        public string Message { get; }
        public string? Url { get; set; }
    }
}

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates that a member is required when initializing an object.
    /// Used for required members (C# 11.0).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property |
                    AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute { }

    /// <summary>
    /// Indicates that compiler features are required for a particular element.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        public string FeatureName { get; }
        public bool IsOptional { get; init; }
    }
}
#endif
