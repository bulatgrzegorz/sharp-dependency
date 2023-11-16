using System.Collections.Concurrent;
using NuGet.Versioning;
using SelectiveConditionEvaluator;
using sharp_dependency.Logger;
using sharp_dependency.Parsers;

namespace sharp_dependency;

//TODO: Should we support prerelease versions here?
public class ProjectMigrator
{
    private readonly IPackageMangerService _packageManager;
    private readonly IProjectDependencyUpdateLogger _logger;
    private readonly ConcurrentDictionary<(string framework, string condition), bool> _conditionEvaluationCache = new();
    private readonly ConcurrentDictionary<string, Lazy<SelectiveParser>> _selectiveParsers = new();

    public ProjectMigrator(IPackageMangerService packageManager, IProjectDependencyUpdateLogger logger)
    {
        _packageManager = packageManager;
        _logger = logger;
    }

    public async Task<UpdateProjectResult> Update(UpdateProjectRequest request)
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
            return new UpdateProjectResult();
        }
        
        _logger.LogProject(request.ProjectPath);
        var migrationActions = new List<MigrationAction>();
        foreach (var dependency in projectFile.Dependencies)
        {
            var migrationInstruction = request.MigrationInstructions.SingleOrDefault(x => x.DependencyName.Equals(dependency.Name, StringComparison.InvariantCultureIgnoreCase));
            if (migrationInstruction is null)
            {
                continue;
            }
            
            var allVersions = await GetPackageVersions(projectTargetFrameworks, dependency, false);
            if (allVersions.Count == 0)
            {
                Log.LogWarn("Could not execute instruction update on {0}. Package could not be find.", migrationInstruction.DependencyName);
                continue;
            }

            if (dependency.UpdateVersionIfPossible(allVersions, migrationInstruction.VersionRange, out var newVersion))
            {
                migrationActions.Add(new MigrationAction(migrationInstruction.DependencyName, dependency.CurrentVersion, newVersion.ToNormalizedString()));
                _logger.LogDependency(dependency.Name, dependency.CurrentVersion, newVersion.ToNormalizedString());
            }
            else
            {
                Log.LogWarn("Could not execute update on {0}. Package with current version {1} was not updated.", migrationInstruction.DependencyName, dependency.CurrentVersion);
            }
        }
        
        var updatedProjectContent = await projectFileParser.Generate();
        _logger.Flush();
        return new UpdateProjectResult(updatedProjectContent, migrationActions);
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
    
    
    public record MigrationInstruction(string DependencyName, VersionRange VersionRange);
    public record MigrationAction(string DependencyName, string CurrentVersion, string NewVersion);
    
    public readonly record struct UpdateProjectRequest(string ProjectPath, string ProjectContent, string? DirectoryBuildProps, IEnumerable<MigrationInstruction> MigrationInstructions);
    public readonly record struct UpdateProjectResult(string? UpdatedContent, List<MigrationAction> UpdatedDependencies);
}