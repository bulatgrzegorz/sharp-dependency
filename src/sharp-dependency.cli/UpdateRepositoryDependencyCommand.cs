using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace sharp_dependency.cli;

internal sealed class UpdateRepositoryDependencyCommand : AsyncCommand<UpdateRepositoryDependencyCommand.Settings>
{
    private readonly BitbucketServerRepositoryManager _bitbucketServerRepository;
    
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-r|--repository")]
        public string? Repository { get; init; }
        
        [CommandOption("-p|--project")]
        public string? Project { get; init; }
        
        [Description("Command will determine dependencies to be updated without actually updating them.")]
        [CommandOption("--dry-run")]
        public bool DryRun { get; init; }
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddUserSecrets<Program>();
        var configuration = configurationBuilder.Build();

        var nugetManager = new NugetPackageSourceMangerChain(
            new NugetPackageSourceManger(),
            new NugetPackageSourceManger(configuration["NugetAddress"]!, NugetPackageSourceManger.ApiVersion.V3, (configuration["NugetUserName"]!, configuration["NugetToken"]!, true)));

        var bitbucketManager = new BitbucketServerRepositoryManager(
            configuration["BitbucketAddress"]!,
            configuration["BitbucketToken"]!, 
            settings.Repository!, 
            settings.Project!);

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
            
            //TODO: Finish editing files in repository and creating PR 
            // if (!settings.DryRun)
            // {
            //     await File.WriteAllTextAsync(projectPath, updatedProjectContent);
            // }
        }
        
        return 0;
    }

    private async Task<IReadOnlyCollection<string>> GetProjectPaths(IReadOnlyCollection<string> filePaths, BitbucketServerRepositoryManager bitbucketServerManager)
    {
        var solutionsPath = FindSolutionPath(filePaths);
        if (solutionsPath is null) return filePaths.Where(FileIsProjectFile).ToList();
        
        var solutionParser = new SolutionFileParser();
        var solutionContent = await bitbucketServerManager.GetFileContent(solutionsPath);
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

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.Repository))
        {
            return ValidationResult.Error($"Setting {nameof(settings.Repository)} must have a value.");
        }
        
        if (string.IsNullOrEmpty(settings.Project))
        {
            return ValidationResult.Error($"Setting {nameof(settings.Project)} must have a value.");
        }
        
        return ValidationResult.Success();
    }
}