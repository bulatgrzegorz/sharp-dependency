﻿using System.ComponentModel;
using sharp_dependency.cli.Logger;
using sharp_dependency.cli.Repositories;
using sharp_dependency.Logger;
using sharp_dependency.Parsers;
using Spectre.Console.Cli;

namespace sharp_dependency.cli.DependencyCommands;

// ReSharper disable ClassNeverInstantiated.Global
internal sealed class ListRepositoryDependencyCommand : RepositoryDependencyCommandBase<ListRepositoryDependencyCommand.Settings>
{
    public class Settings : CommandSettings
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
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var currentConfiguration = await SettingsManager.GetSettings<Configuration>();
        if (currentConfiguration is null)
        {
            Log.LogError("There is no configuration created yet. Use -h|--help for more info.");
            return 1;
        }
        
        var repositoryContext = settings.RepositorySourceName ?? currentConfiguration.CurrentConfiguration?.RepositoryContext;
        if (string.IsNullOrEmpty(repositoryContext))
        {
            Log.LogError("Either repository source name parameter should be used or repository source name as current context.");
            return 1;
        }
        
        if (!currentConfiguration.Bitbuckets.TryGetValue(repositoryContext, out var bitbucket))
        {
            Log.LogError("There is no bitbucket repository configuration for repository source name {0}.", repositoryContext);
            return 1;
        }
        
        var bitbucketManager = BitbucketManagerFactory.CreateRepositoryManager(bitbucket.ApiAddress, settings.Workspace, settings.Project, settings.Repository, bitbucket);

        var repositoryPaths = (await bitbucketManager.GetRepositoryFilePaths()).ToList();
        
        var projectPaths = await GetProjectPaths(repositoryPaths, bitbucketManager);
        
        var logger = new ProjectDependencyLogger();
        foreach (var projectPath in projectPaths)
        {
            logger.LogProject(projectPath);
            
            var projectContent = await bitbucketManager.GetFileContentRaw(projectPath);
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