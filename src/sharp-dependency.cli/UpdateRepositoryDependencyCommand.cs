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
        
        [Description("Name of a branch on which dependencies updates should be commited at.")]
        [CommandOption("-b|--branch")]
        public string? BranchName { get; init; }
        
        [Description("Commit message with which dependencies update commit will going to be done.")]
        [CommandOption("--commitMessage")]
        public string? CommitMessage { get; init; }
        
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
        var workspace = settings.Workspace;
        var project = settings.Project;
        var repository = settings.Repository;
        IRepositoryManger bitbucketManager = (bitbucket, bitbucket.Credentials) switch
        {
            (CloudBitbucket, null) => new BitbucketCloudRepositoryManager( bitbucketAddress, workspace, repository),
            (ServerBitbucket, null) => new BitbucketServerRepositoryManager(bitbucketAddress, repository, project),
            (CloudBitbucket, AppPasswordCredentials c) => new BitbucketCloudRepositoryManager( bitbucketAddress, workspace, repository, (c.UserName, c.AppPassword)),
            (CloudBitbucket, AccessTokenCredentials c) => new BitbucketCloudRepositoryManager(bitbucketAddress, workspace, repository, c.Token),
            (ServerBitbucket, AppPasswordCredentials c) => new BitbucketServerRepositoryManager(bitbucketAddress, repository, project, (c.UserName, c.AppPassword)),
            (ServerBitbucket, AccessTokenCredentials c) => new BitbucketServerRepositoryManager(bitbucketAddress, repository, project, c.Token),
            _ => throw new ArgumentOutOfRangeException()
        };

        var repositoryPaths = (await bitbucketManager.GetRepositoryFilePaths()).ToList();

        var projectPaths = await GetProjectPaths(repositoryPaths, bitbucketManager);

        //TODO: We should check if anything was actually updated in project before
        var results = new List<(string filePath, string updatedContent)>(projectPaths.Count);
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
                results.Add((projectPath, updatedProjectContent));
            }
        }

        var branch = settings.BranchName ?? "sharp-dependency";
        await bitbucketManager.CreateCommit(branch, settings.CommitMessage ?? "update dependencies", results);
        await bitbucketManager.CreatePullRequest(branch, $"[{branch}] pull request", "pr descr");
        
        return 0;
    }

    private async Task<IReadOnlyCollection<string>> GetProjectPaths(IReadOnlyCollection<string> filePaths, IRepositoryManger bitbucketServerManager)
    {
        var solutionsPath = FindSolutionPath(filePaths);
        if (solutionsPath is null) return filePaths.Where(FileIsProjectFile).ToList();
        
        var solutionParser = new SolutionFileParser();
        var solutionContent = await bitbucketServerManager.GetFileContent(solutionsPath);
        // Paths in sln file are using backslash while everywhere else (url, ...) we are going to use forward slash
        return solutionParser.GetProjectPaths(solutionContent).Select(x => x.Replace("\\", "/")).ToList();
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
        
        if (string.IsNullOrEmpty(settings.Project) && string.IsNullOrEmpty(settings.Workspace))
        {
            return ValidationResult.Error($"Neither {nameof(settings.Project)} or {nameof(settings.Workspace)} must have a value (depending of bitbucket type you are using).");
        }

        if (!string.IsNullOrEmpty(settings.BranchName) && settings.BranchName.Length > 255)
        {
            return ValidationResult.Error("Branch name has to be shorter then 255 chars.");
        }
        
        if (!string.IsNullOrEmpty(settings.CommitMessage) && settings.CommitMessage.Length > 72)
        {
            return ValidationResult.Error("Commit message has to be shorter then 72 chars.");
        }
        
        return ValidationResult.Success();
    }
}