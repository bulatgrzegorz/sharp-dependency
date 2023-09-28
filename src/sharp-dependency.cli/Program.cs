using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using sharp_dependency;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.UseStrictParsing();
    config.AddCommand<UpdateSolutionDependencyCommand>("update");
});

await app.RunAsync(args);

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class UpdateSolutionDependencyCommand : AsyncCommand<UpdateSolutionDependencyCommand.UpdateSolutionDependencySettings>
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class UpdateSolutionDependencySettings : CommandSettings 
    {
        [Description("Path to solution which dependency should be updated")]
        [CommandArgument(0, "[solutionPath]")]
        public string? SolutionPath { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSolutionDependencySettings settings)
    {
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddUserSecrets<Program>();

        var configuration = configurationBuilder.Build();
        
        var nugetManager = new NugetPackageSourceMangerChain(
            new NugetPackageSourceManger(),
            new NugetPackageSourceManger(configuration["NugetAddress"]!, NugetPackageSourceManger.ApiVersion.V3, (configuration["NugetUserName"]!, configuration["NugetToken"]!, true)));
        var solutionFileParser = new SolutionFileParser();
        var solutionProjects = solutionFileParser.GetProjectPaths(FileContent.Create(settings.SolutionPath!));

        Console.WriteLine("Found {0} projects in solution {1}", solutionProjects.Count, settings.SolutionPath);
        
        foreach (var solutionProject in solutionProjects)
        {
            Console.WriteLine("{0}", solutionProject);
            
            var projectContent = await File.ReadAllTextAsync(solutionProject);
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
            await File.WriteAllTextAsync(solutionProject, updatedProjectContent);
        }

        return 0;
    }

    public override ValidationResult Validate(CommandContext context, UpdateSolutionDependencySettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SolutionPath))
        {
            return ValidationResult.Error($"Setting {nameof(settings.SolutionPath)} must have a value.");
        }
        
        return ValidationResult.Success();
    }
}