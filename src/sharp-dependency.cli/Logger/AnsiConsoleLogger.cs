using sharp_dependency.Logger;
using Spectre.Console;

namespace sharp_dependency.cli.Logger;

public class AnsiConsoleLogger : ILogger
{
    public void LogInfo(string message)
    {
        AnsiConsole.Markup("[green]Info: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message));
    }

    public void LogInfo(string message, object arg)
    {
        AnsiConsole.Markup("[green]Info: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message), arg);
    }

    public void LogDebug(string message)
    {
        AnsiConsole.Markup("[blue]Debug: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message));
    }

    public void LogWarn(string message)
    {
        AnsiConsole.Markup("[orange]Warn: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message));
    }

    public void LogWarn(string message, object arg)
    {
        AnsiConsole.Markup("[orange]Warn: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message), arg);
    }

    public void LogWarn(string message, object arg1, object arg2)
    {
        AnsiConsole.Markup("[orange]Warn: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message), arg1, arg2);
    }

    public void LogError(string message)
    {
        AnsiConsole.Markup("[red]Error: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message));
    }

    public void LogError(string message, object arg)
    {
        AnsiConsole.Markup("[red]Error: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(message), arg);
    }
}