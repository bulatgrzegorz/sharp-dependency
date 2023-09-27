using System.ComponentModel;
using Microsoft.Extensions.Configuration;
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
        
        var onpremiseNugetManager = new OnPremiseNugetPackageSourceManger(configuration["NugetAddress"]!, configuration["NugetToken"]!);
        var nugetManager = new NugetPackageSourceManger();
        var solutionFileParser = new SolutionFileParser();
        var solutionProjects = solutionFileParser.GetProjectPaths(FileContent.Create(settings.SolutionPath!));

        Console.WriteLine("Found {0} projects in solution {1}", solutionProjects.Count, settings.SolutionPath);
        
        var latestDependencyVersionCache = new Dictionary<string, string?>();
        foreach (var solutionProject in solutionProjects)
        {
            Console.WriteLine("Updating dependencies in project: {0}", solutionProject);
            
            var projectContent = await File.ReadAllTextAsync(solutionProject);
            await using var projectFileParser = new ProjectFileParser(projectContent);
            var projectFile = await projectFileParser.Parse();
            foreach (var dependency in projectFile.Dependencies)
            {
                if (!latestDependencyVersionCache.TryGetValue(dependency.Name, out var latestVersion))
                {
                    latestVersion = await nugetManager.GetLatestPackageVersions(dependency.Name);
                }
                
                // if (latestVersion is null)
                // {
                //     var onPremiseLatestVersion = await onpremiseNugetManager.GetLatestPackageVersionsV2(dependency.Name);
                //     if (onPremiseLatestVersion is null)
                //     {
                //         Console.WriteLine("ERROR: Could not determine latest version for package: {0}.", dependency.Name);
                //         latestDependencyVersionCache.Add(dependency.Name, null);
                //         continue;
                //     }
                //     
                //     latestDependencyVersionCache.Add(dependency.Name, onPremiseLatestVersion);
                //     latestVersion = onPremiseLatestVersion;
                // }
                if (latestVersion is null)
                {
                    latestDependencyVersionCache.Add(dependency.Name, null);
                    Console.WriteLine("ERROR: Could not determine latest version for package: {0}.", dependency.Name);
                    continue;
                }
        
                //TODO: Fix comparing - is it enough?
                if (dependency.CurrentVersion != latestVersion)
                {
                    Console.WriteLine("Update dependency {0}({1}) with latest version {2}", dependency.Name, dependency.CurrentVersion, latestVersion);
                    
                    dependency.UpdateVersion(latestVersion);
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