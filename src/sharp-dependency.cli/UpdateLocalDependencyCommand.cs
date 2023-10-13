using System.ComponentModel;
using NuGet.Configuration;
using SelectiveConditionEvaluator;
using sharp_dependency.Parsers;
using sharp_dependency.Repositories;
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
        var currentConfiguration = await SettingsManager.GetSettings<Configuration>();
        if (currentConfiguration is null)
        {
            Console.WriteLine("[ERROR]: There is no configuration created yet. Use -h|--help for more info.");
            return 1;
        }

        if (currentConfiguration.NugetConfiguration is null)
        {
            Console.WriteLine("[ERROR]: There is no nuget configuration created yet. Use -h|--help for more info.");
            return 1;
        }
        
        var packageSourceProvider = new PackageSourceProvider(new NuGet.Configuration.Settings(currentConfiguration.NugetConfiguration.ConfigFileDirectory, currentConfiguration.NugetConfiguration.ConfigFileName));
        var packageSources = packageSourceProvider.LoadPackageSources().ToList();
        if (packageSources is { Count: 0 })
        {
            Console.WriteLine("[ERROR]: Given nuget configuration has no package sources. We cannot determine any package version using it.");
            return 1;
        }

        var nugetManager = new NugetPackageSourceMangerChain(packageSources.Select(x => new NugetPackageSourceManger(x)).ToArray());

        var projectPaths = GetProjectsPath(settings);

        foreach (var projectPath in projectPaths)
        {
            Console.WriteLine("{0}", projectPath);
            
            var projectContent = await File.ReadAllTextAsync(projectPath);
            await using var projectFileParser = new ProjectFileParser(projectContent);
            var projectFile = await projectFileParser.Parse();
            foreach (var dependency in projectFile.Dependencies)
            {
                var targetFrameworks = new List<string>();
                if (dependency.Conditions.Length > 0)
                {
                    foreach (var targetFramework in projectFile.TargetFrameworks)
                    {
                        var conditionParser = new SelectiveParser("TargetFramework", targetFramework);
                        foreach (var dependencyCondition in dependency.Conditions)
                        {
                            //TODO: Cache evaluation of given condition with given target framework (there could be same condition for multiple packages in itemGroup.
                            if (conditionParser.EvaluateSelective(dependencyCondition))
                            {
                                targetFrameworks.Add(targetFramework);
                            }
                        }
                    }                
                }
                else
                {
                    targetFrameworks = projectFile.TargetFrameworks.ToList();
                }

                var allVersions = await nugetManager.GetPackageVersions(dependency.Name, targetFrameworks);
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