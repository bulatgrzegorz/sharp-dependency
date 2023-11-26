using System.ComponentModel;
using sharp_dependency.cli.Logger;
using sharp_dependency.Parsers;
using Spectre.Console.Cli;

namespace sharp_dependency.cli.DependencyCommands;

internal sealed class ListLocalDependencyCommand : LocalDependencyCommandBase<ListLocalDependencyCommand.Settings>
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Settings : CommandSettings
    {
        [Description("Path to solution/csproj which dependency should be updated")]
        [CommandArgument(0, "[path]")]
        public string? Path { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var logger = new ProjectDependencyLogger();
        
        //TODO: Gather dependencies from directory build props files
        var (_, projectPaths, _) = GetRepositoryFiles(settings.Path);
        foreach (var projectPath in projectPaths)
        {
            logger.LogProject(projectPath);
            
            var projectContent = await File.ReadAllTextAsync(projectPath);

            await using var projectFileParser = new ProjectFileParser(projectContent);
            var projectFile = await projectFileParser.Parse();
            
            foreach (var dependency in projectFile.Dependencies)
            {
                logger.LogDependency(dependency.Name, dependency.CurrentVersion);   
            }
            
            logger.Flush();
        }

        return 0;
    }
}