using System.Diagnostics.CodeAnalysis;

namespace sharp_dependency.Logger;

public static class Log
{
    public static ILogger Logger { get; set; } = null!;

    public static void LogInfo(string message) => Logger.LogInfo(message);
    public static void LogInfo([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg) => Logger.LogInfo(message, arg);

    public static void LogDebug(string message) => Logger.LogDebug(message);
    public static void LogDebug([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg) => Logger.LogDebug(message, arg);
    public static void LogDebug([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg1, object arg2) => Logger.LogDebug(message, arg1, arg2);
    public static void LogWarn(string message) => Logger.LogWarn(message);
    public static void LogWarn([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg) => Logger.LogWarn(message, arg);
    public static void LogWarn([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg1, object arg2) => Logger.LogWarn(message, arg1, arg2);

    public static void LogError(string message) => Logger.LogError(message);
    public static void LogError([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg) => Logger.LogError(message, arg);
    public static void LogError([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg1, object arg2) => Logger.LogError(message, arg1, arg2);
}