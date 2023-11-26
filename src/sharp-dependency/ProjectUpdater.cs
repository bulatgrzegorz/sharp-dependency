using System.Collections.Concurrent;
using NuGet.Versioning;
using SelectiveConditionEvaluator;
using sharp_dependency.Logger;
using sharp_dependency.Parsers;
using sharp_dependency.Repositories;
using Dependency = sharp_dependency.Parsers.Dependency;

namespace sharp_dependency;

public class ProjectUpdater
{
    private readonly IPackageMangerService _packageManager;
    private readonly ConcurrentDictionary<string, Lazy<SelectiveParser>> _selectiveParsers = new();
    private readonly ConcurrentDictionary<(string framework, string condition), bool> _conditionEvaluationCache = new();
    private readonly IProjectDependencyUpdateLogger _logger;

    public ProjectUpdater(IPackageMangerService packageManager, IProjectDependencyUpdateLogger logger)
    {
        _packageManager = packageManager;
        _logger = logger;
    }
    
    public async Task<UpdatedProject?> Update(UpdateProjectRequest request)
    {
        Guard.ThrowIfNullOrWhiteSpace(request.ProjectContent);

        await using var directoryBuildPropsParser = request.DirectoryBuildProps is not null ? new ProjectFileParser(request.DirectoryBuildProps) : null;
        var directoryBuildPropsFile = await (directoryBuildPropsParser?.Parse() ?? Task.FromResult((ProjectFile?)default)!);
        await using var projectFileParser = new ProjectFileParser(request.ProjectContent);
        var projectFile = await projectFileParser.Parse();
        
        var projectTargetFrameworks = projectFile.TargetFrameworks is { Count: > 0 }
            ? projectFile.TargetFrameworks
            : directoryBuildPropsFile?.TargetFrameworks;

        if (projectTargetFrameworks is null or { Count: 0 })
        {
            Log.LogWarn("Could not determine target framework for project: {0}", request.ProjectPath);
            return null;
        }

        _logger.LogProject(request.ProjectPath);

        var updatedDependencies = new List<sharp_dependency.Repositories.Dependency>();
        foreach (var dependency in projectFile.Dependencies)
        {
            //TODO: We should be also consider directoryBuildProps dependencies here as well. Project file can not determine dependency version for example.

            var includePrerelease = false;
            var allVersions = await GetPackageVersions(projectTargetFrameworks, dependency, includePrerelease);
            if (allVersions.Count == 0)
            {
                continue;
            }

            if (dependency.UpdateVersionIfPossible(allVersions, request.IncludePrerelease, request.VersionLock, out var newVersion))
            {
                updatedDependencies.Add(new sharp_dependency.Repositories.Dependency(dependency.Name, dependency.CurrentVersion, newVersion.ToNormalizedString()));
                _logger.LogDependency(dependency.Name, dependency.CurrentVersion, newVersion.ToNormalizedString());   
            }
        }

        var updatedProjectContent = await projectFileParser.Generate();
        _logger.Flush();
        return new UpdatedProject(request.ProjectPath, updatedProjectContent, updatedDependencies);
    }

    private async Task<IReadOnlyCollection<NuGetVersion>> GetPackageVersions(IReadOnlyCollection<string> projectTargetFrameworks, Dependency dependency, bool includePrerelease)
    {
        if (dependency.Conditions.Length <= 0)
        {
            return await _packageManager.GetPackageVersions(dependency.Name, projectTargetFrameworks, includePrerelease);
        }
        
        var targetFrameworks = new List<string>();
        foreach (var targetFramework in projectTargetFrameworks)
        {
            foreach (var dependencyCondition in dependency.Conditions)
            {
                if (EvaluateCondition(targetFramework, dependencyCondition))
                {
                    targetFrameworks.Add(targetFramework);
                }
            }
        }

        return await _packageManager.GetPackageVersions(dependency.Name, targetFrameworks);
    }

    private bool EvaluateCondition(string framework, string condition)
    {
        return _conditionEvaluationCache.GetOrAdd((framework, condition), key =>
        {
            var parser = _selectiveParsers
                .GetOrAdd(key.framework, f => 
                    new Lazy<SelectiveParser>(() => 
                        new SelectiveParser("TargetFramework", f)));
            return parser.Value.EvaluateSelective(key.condition);
        });
    }

    public readonly record struct UpdateProjectRequest(string ProjectPath, string ProjectContent, string? DirectoryBuildProps, bool IncludePrerelease, VersionLock VersionLock);
}