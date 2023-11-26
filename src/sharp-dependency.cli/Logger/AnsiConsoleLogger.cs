using sharp_dependency.Logger;
using Spectre.Console;

namespace sharp_dependency.cli.Logger;

public class AnsiConsoleLogger : ILogger
{
    private readonly LogLevel _minimalLogLevel;

    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }
    
    public AnsiConsoleLogger(LogLevel minimalLogLevel)
    {
        _minimalLogLevel = minimalLogLevel;
    }
    
    public void LogDebug(string message)
    {
        if(_minimalLogLevel > LogLevel.Debug) return;

        AnsiConsole.Markup("[blue]Debug: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message));
    }
    
    public void LogDebug(string message, object arg)
    {
        if(_minimalLogLevel > LogLevel.Debug) return;
        
        AnsiConsole.Markup("[blue]Debug: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message), arg);
    }
    
    public void LogDebug(string message, object arg1, object arg2)
    {
        if(_minimalLogLevel > LogLevel.Debug) return;
        
        AnsiConsole.Markup("[blue]Debug: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message), arg1, arg2);
    }

    public void LogInfo(string message)
    {
        if(_minimalLogLevel > LogLevel.Info) return;
        
        AnsiConsole.Markup("[green]Info: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message));
    }

    public void LogInfo(string message, object arg)
    {
        if(_minimalLogLevel > LogLevel.Info) return;
        
        AnsiConsole.Markup("[green]Info: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message), arg);
    }
    
    public void LogWarn(string message)
    {
        if(_minimalLogLevel > LogLevel.Warn) return;
        
        AnsiConsole.Markup("[orange3]Warn: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message));
    }

    public void LogWarn(string message, object arg)
    {
        if(_minimalLogLevel > LogLevel.Warn) return;
        
        AnsiConsole.Markup("[orange3]Warn: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message), arg);
    }

    public void LogWarn(string message, object arg1, object arg2)
    {
        if(_minimalLogLevel > LogLevel.Warn) return;
        
        AnsiConsole.Markup("[orange3]Warn: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message), arg1, arg2);
    }

    public void LogError(string message)
    {
        if(_minimalLogLevel > LogLevel.Error) return;
        
        AnsiConsole.Markup("[red]Error: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message));
    }

    public void LogError(string message, object arg)
    {
        if(_minimalLogLevel > LogLevel.Error) return;
        
        AnsiConsole.Markup("[red]Error: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message), arg);
    }
    
    public void LogError(string message, object arg1, object arg2)
    {
        if(_minimalLogLevel > LogLevel.Error) return;
        
        AnsiConsole.Markup("[red]Error: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message), arg1, arg2);
    }
}