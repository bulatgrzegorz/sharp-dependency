using System.Collections.Concurrent;
using NuGet.Versioning;
using SelectiveConditionEvaluator;
using sharp_dependency.Parsers;

namespace sharp_dependency;

public class ProjectUpdater
{
    private readonly IPackageMangerService _packageManager;
    private readonly ConcurrentDictionary<string, Lazy<SelectiveParser>> _selectiveParsers = new();
    private readonly ConcurrentDictionary<(string framework, string condition), bool> _conditionEvaluationCache = new();

    public ProjectUpdater(IPackageMangerService packageManager)
    {
        _packageManager = packageManager;
    }
    
    public async Task<UpdateProjectResult> Update(UpdateProjectRequest request)
    {
        Guard.ThrowIfNullOrWhiteSpace(request.ProjectContent);
        
        await using var projectFileParser = new ProjectFileParser(request.ProjectContent);
        var projectFile = await projectFileParser.Parse();
        
        foreach (var dependency in projectFile.Dependencies)
        {
            var allVersions = await GetPackageVersions(projectFile, dependency);
            if (allVersions.Count == 0)
            {
                continue;
            }

            if (dependency.UpdateVersionIfPossible(allVersions, out var newVersion))
            {
                //TODO: If we want to leave updating packages here, we need to also push here some kind of Logger with methods as: LogDependencyUpdates
                Console.WriteLine("     {0} {1} -> {2}", dependency.Name, dependency.CurrentVersion, newVersion);    
            }
        }

        var updatedProjectContent = await projectFileParser.Generate();
        return new UpdateProjectResult(updatedProjectContent);
    }

    private async Task<IReadOnlyCollection<NuGetVersion>> GetPackageVersions(ProjectFile projectFile, Dependency dependency)
    {
        if (dependency.Conditions.Length <= 0)
        {
            return await _packageManager.GetPackageVersions(dependency.Name, projectFile.TargetFrameworks);
        }
        
        var targetFrameworks = new List<string>();
        foreach (var targetFramework in projectFile.TargetFrameworks)
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

    public readonly record struct UpdateProjectRequest(string ProjectContent);
    public readonly record struct UpdateProjectResult(string UpdatedContent);
}