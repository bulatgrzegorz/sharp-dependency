using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace System;

public static class Guard
{
    public static void ThrowIfNullOrWhiteSpace(string parameter, [CallerArgumentExpression(nameof(parameter))] string callerMethodArgName = "")
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new ArgumentException($"Parameter {callerMethodArgName} must have value.", callerMethodArgName);
        }
    }
}