using System.Diagnostics.CodeAnalysis;

namespace sharp_dependency.Logger;

public interface ILogger
{
    void LogDebug(string message);
    void LogDebug(string message, object arg);
    void LogDebug(string message, object arg1, object arg2);
    void LogInfo(string message);
    void LogInfo([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg);
    
    void LogWarn(string message);
    void LogWarn([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg);
    void LogWarn([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg1, object arg2);
    void LogError(string message);
    void LogError([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg);
    void LogError([StringSyntax(StringSyntaxAttribute.CompositeFormat)]string message, object arg1, object arg2);
}