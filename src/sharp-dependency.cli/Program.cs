using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using sharp_dependency;
using Spectre.Console;
using Spectre.Console.Cli;

foreach (var a in Enum.GetValues<Environment.SpecialFolder>())
{
    Console.WriteLine($"{Enum.GetName(a)}: {Environment.GetFolderPath(a)}");
}


var app = new CommandApp();
app.Configure(config =>
{
    config.UseStrictParsing();
    config.AddBranch("update", x =>
    {
        x.AddCommand<UpdateLocalDependencyCommand>("local");
        x.AddCommand<UpdateRepositoryDependencyCommand>("repo");
    });
    // config.AddCommand<UpdateDependencyCommand>("update local");
    // config.AddCommand<UpdateDependencyCommand>("update repo");
});

await app.RunAsync(args);

internal sealed class UpdateRepositoryDependencyCommand : AsyncCommand<UpdateRepositoryDependencyCommand.Settings>
{
    private readonly BitbucketRepositoryManager _bitbucketRepository;
    
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-r|--repository")]
        public string? Repository { get; init; }
        
        [CommandOption("-p|--project")]
        public string? Project { get; init; }
    }

    public UpdateRepositoryDependencyCommand()
    {
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddUserSecrets<Program>();

        var configuration = configurationBuilder.Build();
        
        var nugetManager = new NugetPackageSourceMangerChain(
            new NugetPackageSourceManger(),
            new NugetPackageSourceManger(configuration["NugetAddress"]!, NugetPackageSourceManger.ApiVersion.V3, (configuration["NugetUserName"]!, configuration["NugetToken"]!, true)));

        
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddUserSecrets<Program>();
        var configuration = configurationBuilder.Build();

        var nugetManager = new NugetPackageSourceMangerChain(
            new NugetPackageSourceManger(),
            new NugetPackageSourceManger(configuration["NugetAddress"]!, NugetPackageSourceManger.ApiVersion.V3, (configuration["NugetUserName"]!, configuration["NugetToken"]!, true)));

        var bitbucketManager = new BitbucketRepositoryManager(
            configuration["BitbucketAddress"]!,
            configuration["BitbucketToken"]!, 
            settings.Repository, 
            settings.Project);

        var repositoryPaths = (await bitbucketManager.GetRepositoryFilePaths()).ToList();

        var projectPaths = await GetProjectPaths(repositoryPaths, bitbucketManager);
        foreach (var projectPath in projectPaths)
        {
            Console.WriteLine("{0}", projectPath);

            var projectContent = await bitbucketManager.GetFileContentRaw(projectPath);
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
            
            // if (!settings.DryRun)
            // {
            //     await File.WriteAllTextAsync(projectPath, updatedProjectContent);
            // }
        }
        
        return 0;
    }

    private async Task<IReadOnlyCollection<string>> GetProjectPaths(IReadOnlyCollection<string> filePaths, BitbucketRepositoryManager bitbucketManager)
    {
        var solutionsPath = FindSolutionPath(filePaths);
        if (solutionsPath is null) return filePaths.Where(FileIsProjectFile).ToList();
        
        var solutionParser = new SolutionFileParser();
        var solutionContent = await bitbucketManager.GetFileContent(solutionsPath);
        return solutionParser.GetProjectPaths(solutionContent);
    }

    private string? FindSolutionPath(IReadOnlyCollection<string> filePaths)
    {
        var solutionsPaths = FindSolutionFiles(filePaths);
        if (solutionsPaths.Count == 0)
        {
            return null;
        }

        return FindSolutionPathSrcLevel(solutionsPaths) ?? FindSolutionPathRootLevel(solutionsPaths) ?? solutionsPaths.First();
    }

    private IReadOnlyCollection<string> FindSolutionFiles(IReadOnlyCollection<string> filePaths)
    {
        return filePaths.Where(FileIsSln).ToList();
    }

    private string? FindSolutionPathRootLevel(IReadOnlyCollection<string> solutionPaths)
    {
        return solutionPaths.FirstOrDefault(DirectoryIsEmpty);
    }
    
    private string? FindSolutionPathSrcLevel(IReadOnlyCollection<string> solutionPaths)
    {
        return solutionPaths.FirstOrDefault(DirectoryIsSrc);
    }

    private static bool DirectoryIsEmpty(string path) => string.IsNullOrEmpty(Path.GetDirectoryName(path));
    private static bool DirectoryIsSrc(string path) => Path.GetDirectoryName(path)?.Equals("src", StringComparison.InvariantCultureIgnoreCase) ?? false;
    private static bool FileIsProjectFile(string path) => path.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase);
    private static bool FileIsSln(string path) => path.EndsWith(".sln", StringComparison.InvariantCultureIgnoreCase);
}

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