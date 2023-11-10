using System.Collections.Concurrent;
using NuGet.Versioning;
using SelectiveConditionEvaluator;
using sharp_dependency.Logger;
using sharp_dependency.Parsers;

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
            Console.WriteLine("Could not determine target framework for project: {0}", request.ProjectPath);
            return new UpdateProjectResult();
        }

        _logger.LogProject(request.ProjectPath);

        var updatedDependencies = new List<(string name, string currentVersion, string newVersion)>();
        foreach (var dependency in projectFile.Dependencies)
        {
            //TODO: We should be also consider directoryBuildProps dependencies here as well. Project file can not determine dependency version for example.
            
            var allVersions = await GetPackageVersions(projectTargetFrameworks, dependency);
            if (allVersions.Count == 0)
            {
                continue;
            }

            if (dependency.UpdateVersionIfPossible(allVersions, out var newVersion))
            {
                updatedDependencies.Add((dependency.Name, dependency.CurrentVersion, newVersion.ToNormalizedString()));
                _logger.LogDependency(dependency.Name, dependency.CurrentVersion, newVersion.ToNormalizedString());   
            }
        }

        var updatedProjectContent = await projectFileParser.Generate();
        _logger.Flush();
        return new UpdateProjectResult(updatedProjectContent, updatedDependencies);
    }

    private async Task<IReadOnlyCollection<NuGetVersion>> GetPackageVersions(IReadOnlyCollection<string> projectTargetFrameworks, Dependency dependency)
    {
        if (dependency.Conditions.Length <= 0)
        {
            return await _packageManager.GetPackageVersions(dependency.Name, projectTargetFrameworks);
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

    public readonly record struct UpdateProjectRequest(string ProjectPath, string ProjectContent, string? DirectoryBuildProps);
    public readonly record struct UpdateProjectResult(string? UpdatedContent, List<(string name, string currentVersion, string newVersion)> UpdatedDependencies);
}