using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace sharp_dependency.cli;

// ReSharper disable once ClassNeverInstantiated.Global
// ReSharper disable once UnusedAutoPropertyAccessor.Global
internal sealed class UpdateLocalDependencyCommand : AsyncCommand<UpdateLocalDependencyCommand.Settings>
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Settings : CommandSettings 
    {
        [Description("Path to solution/csproj which dependency should be updated")]
        [CommandArgument(0, "[path]")]
        public string? Path { get; init; }

        [Description("Command will determine dependencies to be updated without actually updating them.")]
        [CommandOption("--dry-run")]
        public bool DryRun { get; init; }

        public bool IsPathSolutionFile => Path!.EndsWith(".sln", StringComparison.InvariantCultureIgnoreCase);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddUserSecrets<Program>();

        var configuration = configurationBuilder.Build();
        
        var nugetManager = new NugetPackageSourceMangerChain(
            new NugetPackageSourceManger(),
            new NugetPackageSourceManger(configuration["NugetAddress"]!, NugetPackageSourceManger.ApiVersion.V3, (configuration["NugetUserName"]!, configuration["NugetToken"]!, true)));

        var projectPaths = GetProjectsPath(settings);

        foreach (var projectPath in projectPaths)
        {
            Console.WriteLine("{0}", projectPath);
            
            var projectContent = await File.ReadAllTextAsync(projectPath);
            await using var projectFileParser = new ProjectFileParser(projectContent);
            var projectFile = await projectFileParser.Parse();
            foreach (var dependency in projectFile.Dependencies)
            {
                var allVersions = await nugetManager.GetPackageVersions(dependency.Name, projectFile.TargetFrameworks);
                if (allVersions.Count == 0)
                {
                    continue;
                }

                if (dependency.UpdateVersionIfPossible(allVersions, out var newVersion))
                {
                    Console.WriteLine("     {0} {1} -> {2}", dependency.Name, dependency.CurrentVersion, newVersion);    
                }
            }
            
            var updatedProjectContent = await projectFileParser.Generate();
            
            if (!settings.DryRun)
            {
                await File.WriteAllTextAsync(projectPath, updatedProjectContent);
            }
        }

        return 0;
    }

    private IReadOnlyCollection<string> GetProjectsPath(Settings settings)
    {
        if (!settings.IsPathSolutionFile) return new[] { settings.Path! };
        
        var solutionFileParser = new SolutionFileParser();
        return solutionFileParser.GetProjectPaths(FileContent.CreateFromLocalPath(settings.Path!));
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Path))
        {
            return ValidationResult.Error($"Setting {nameof(settings.Path)} must have a value.");
        }

        if (!settings.Path.EndsWith(".sln", StringComparison.InvariantCultureIgnoreCase) &&
            !settings.Path.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase))
        {
            return ValidationResult.Error($"Setting {nameof(settings.Path)} must be either solution (sln) or project (csproj) file.");
        }
        
        return ValidationResult.Success();
    }
}