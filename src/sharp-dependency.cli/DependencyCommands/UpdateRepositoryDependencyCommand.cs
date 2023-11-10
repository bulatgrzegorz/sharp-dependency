﻿using System.ComponentModel;
using sharp_dependency.cli.Logger;
using sharp_dependency.Parsers;
using sharp_dependency.Repositories;
using sharp_dependency.Repositories.Bitbucket;
using Spectre.Console;
using Spectre.Console.Cli;
using CloudBitbucket = sharp_dependency.cli.Configuration.Bitbucket.CloudBitbucket;
using ServerBitbucket = sharp_dependency.cli.Configuration.Bitbucket.ServerBitbucket;
using AppPasswordCredentials = sharp_dependency.cli.Configuration.Bitbucket.BitbucketCredentials.AppPasswordBitbucketCredentials;
using AccessTokenCredentials = sharp_dependency.cli.Configuration.Bitbucket.BitbucketCredentials.AccessTokenBitbucketCredentials;
using Dependency = sharp_dependency.Repositories.Dependency;

namespace sharp_dependency.cli.DependencyCommands;

internal sealed class UpdateRepositoryDependencyCommand : RepositoryDependencyCommandBase<UpdateRepositoryDependencyCommand.Settings>
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
        
        [Description("Command will look for pre-release versions of packages.")]
        [CommandOption("--prerelease")]
        [DefaultValue(false)]
        public bool IncludePrerelease { get; init; }
        
        [Description("Specifies whether the package should be locked to the current Major or Minor version. Possible values: None (default), Major, Minor")]
        [CommandOption("-v|--version-lock")]
        [DefaultValue(VersionLock.None)]
        public VersionLock VersionLock { get; init; }
        
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
        
        var nugetManager = currentConfiguration.GetNugetManager();
        var bitbucket = currentConfiguration.GetBitbucket(settings.RepositorySourceName);
        if (bitbucket is null)
        {
            return 1;
        }

        var bitbucketAddress = bitbucket.ApiAddress;
        var workspace = settings.Workspace;
        var project = settings.Project;
        var repository = settings.Repository;
        IRepositoryManger bitbucketManager = (bitbucket, bitbucket.Credentials) switch
        {
            (CloudBitbucket, null) => new BitbucketCloudRepositoryManager( bitbucketAddress, workspace!, repository),
            (ServerBitbucket, null) => new BitbucketServerRepositoryManager(bitbucketAddress, repository, project!),
            (CloudBitbucket, AppPasswordCredentials c) => new BitbucketCloudRepositoryManager( bitbucketAddress, workspace!, repository, (c.UserName, c.AppPassword)),
            (CloudBitbucket, AccessTokenCredentials c) => new BitbucketCloudRepositoryManager(bitbucketAddress, workspace!, repository, c.Token),
            (ServerBitbucket, AppPasswordCredentials c) => new BitbucketServerRepositoryManager(bitbucketAddress, repository, project!, (c.UserName, c.AppPassword)),
            (ServerBitbucket, AccessTokenCredentials c) => new BitbucketServerRepositoryManager(bitbucketAddress, repository, project!, c.Token),
            _ => throw new ArgumentOutOfRangeException()
        };

        var repositoryPaths = (await bitbucketManager.GetRepositoryFilePaths()).ToList();

        var projectUpdater = new ProjectUpdater(nugetManager, new ProjectDependencyUpdateLogger());
        
        var projectPaths = await GetProjectPaths(repositoryPaths, bitbucketManager);

        //TODO: We should check if anything was actually updated in project before
        //TODO: Refactor loggers/response from project updater
        var projectUpdatedDependencies = new List<(Project project, string updatedContent)>(projectPaths.Count);
        foreach (var projectPath in projectPaths)
        {
            var directoryBuildPropsFile = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(repositoryPaths, projectPath);
            var projectContent = await bitbucketManager.GetFileContentRaw(projectPath);
            var directoryBuildPropsContent = directoryBuildPropsFile is not null ? await bitbucketManager.GetFileContentRaw(directoryBuildPropsFile) : null;
            var updatedProject = await projectUpdater.Update(new ProjectUpdater.UpdateProjectRequest(projectPath, projectContent, directoryBuildPropsContent, settings.IncludePrerelease, settings.VersionLock));
         
            projectUpdatedDependencies.Add(
                (new Project()
                {
                    Name = projectPath, 
                    
                    UpdatedDependencies = updatedProject.UpdatedDependencies.Select(x => new Dependency(){Name = x.name, CurrentVersion = x.currentVersion, NewVersion = x.newVersion}).ToList()
                }, updatedProject.UpdatedContent!));
        }

        if (settings.DryRun)
        {
            return 0;
        }

        var branch = settings.BranchName ?? "sharp-dependency";
        await bitbucketManager.CreateCommit(branch, settings.CommitMessage ?? "update dependencies", projectUpdatedDependencies.Select(x => (x.project.Name, x.updatedContent)).ToList());
        
        var description = new Description()
        {
            UpdatedProjects = projectUpdatedDependencies.Select(x => x.project).ToList()
        };
        
        await bitbucketManager.CreatePullRequest(new CreatePullRequest(){Name = $"[{branch}] pull request", SourceBranch = branch, Description = description});
        
        return 0;
    }
    
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