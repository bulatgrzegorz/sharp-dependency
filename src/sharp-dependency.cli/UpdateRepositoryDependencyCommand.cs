using System.ComponentModel;
using NuGet.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using CloudBitbucket = sharp_dependency.cli.Configuration.Bitbucket.CloudBitbucket;
using ServerBitbucket = sharp_dependency.cli.Configuration.Bitbucket.ServerBitbucket;
using AppPasswordCredentials = sharp_dependency.cli.Configuration.Bitbucket.BitbucketCredentials.AppPasswordBitbucketCredentials;
using AccessTokenCredentials = sharp_dependency.cli.Configuration.Bitbucket.BitbucketCredentials.AccessTokenBitbucketCredentials;

namespace sharp_dependency.cli;

internal sealed class UpdateRepositoryDependencyCommand : AsyncCommand<UpdateRepositoryDependencyCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Name of repository source that should be used. If not given, current context will be used.")]
        [CommandOption("-n|--name")]
        public string? RepositorySourceName { get; init; }
        
        [Description("Name of repository that we should update dependencies in.")]
        [CommandOption("-r|--repository")]
        public string Repository { get; init; } = null!;

        [Description("Name of a project within which we should update repositorty. Value is required for Server Bitbucket source type.")]
        [CommandOption("-p|--project")]
        public string? Project { get; init; }
        
        [Description("Name of a workspace within which we should update repositorty. Value is required for Cloud Bitbucket source type.")]
        [CommandOption("-w|--workspace")]
        public string? Workspace { get; init; }
        
        [Description("Command will determine dependencies to be updated without actually updating them.")]
        [CommandOption("--dry-run")]
        public bool DryRun { get; init; }
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

        if (currentConfiguration.Bitbuckets is { Count: 0 })
        {
            Console.WriteLine("[ERROR]: Given bitbucket configuration has no repository sources.");
            return 1;
        }

        var repositoryContext = settings.RepositorySourceName ?? currentConfiguration.CurrentConfiguration?.RepositoryContext;
        if (string.IsNullOrEmpty(repositoryContext))
        {
            Console.WriteLine("[ERROR]: Either repository source name parameter should be used or repository source name as current context.");
            return 1;
        }

        if (!currentConfiguration.Bitbuckets.TryGetValue(repositoryContext, out var bitbucket))
        {
            Console.WriteLine("[ERROR]: There is no bitbucket repository configuration for repository source name {0}.", repositoryContext);
            return 1;
        }

        var bitbucketAddress = bitbucket.ApiAddress;
        IRepositoryManger bitbucketManager = (bitbucket, bitbucket.Credentials) switch
        {
            (CloudBitbucket conf, null) => new BitbucketCloudRepositoryManager( bitbucketAddress, settings.Workspace, settings.Repository),
            (ServerBitbucket conf, null) => new BitbucketServerRepositoryManager(bitbucketAddress, settings.Repository, settings.Project),
            (CloudBitbucket conf, AppPasswordCredentials credentials) => new BitbucketCloudRepositoryManager( bitbucketAddress, settings.Workspace, settings.Repository, (credentials.UserName, credentials.AppPassword)),
            (CloudBitbucket conf, AccessTokenCredentials credentials) => new BitbucketCloudRepositoryManager(bitbucketAddress, settings.Workspace, settings.Repository, credentials.Token),
            (ServerBitbucket conf, AppPasswordCredentials credentials) => new BitbucketServerRepositoryManager(bitbucketAddress, settings.Repository, settings.Project, (credentials.UserName, credentials.AppPassword)),
            (ServerBitbucket conf, AccessTokenCredentials credentials) => new BitbucketServerRepositoryManager(bitbucketAddress, settings.Repository, settings.Project, credentials.Token),
            _ => throw new ArgumentOutOfRangeException()
        };

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
            if (!settings.DryRun)
            {
                // await bitbucketManager.EditFile(projectPath, updatedProjectContent);
            }
        }
        
        return 0;
    }

    private async Task<IReadOnlyCollection<string>> GetProjectPaths(IReadOnlyCollection<string> filePaths, IRepositoryManger bitbucketServerManager)
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
        
        // if (string.IsNullOrEmpty(settings.Project))
        // {
        //     return ValidationResult.Error($"Setting {nameof(settings.Project)} must have a value.");
        // }
        
        return ValidationResult.Success();
    }
}