using NuGet.Versioning;
using SelectiveConditionEvaluator;
using sharp_dependency.Parsers;

namespace sharp_dependency;

public class ProjectUpdater
{
    private readonly IPackageMangerService _packageManager;

    public ProjectUpdater(IPackageMangerService packageManager)
    {
        _packageManager = packageManager;
    }
    
    public async Task<UpdateProjectResult> Update(UpdateProjectRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectContent);
        
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
            var conditionParser = new SelectiveParser("TargetFramework", targetFramework);
            foreach (var dependencyCondition in dependency.Conditions)
            {
                //TODO: Cache evaluation of given condition with given target framework (there could be same condition for multiple packages in itemGroup.
                if (conditionParser.EvaluateSelective(dependencyCondition))
                {
                    targetFrameworks.Add(targetFramework);
                }
            }
        }

        return await _packageManager.GetPackageVersions(dependency.Name, targetFrameworks);

    }

    public readonly record struct UpdateProjectRequest(string ProjectContent);
    public readonly record struct UpdateProjectResult(string UpdatedContent);
}