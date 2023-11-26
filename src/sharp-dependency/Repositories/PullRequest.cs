// ReSharper disable UnusedAutoPropertyAccessor.Global

#pragma warning disable CS8618
namespace sharp_dependency.Repositories;

public class PullRequest
{
    public int Id { get; set; }
}

public class CreatePullRequest
{
    public string Name { get; set; }
    public string SourceBranch { get; set; }
    public Description Description { get; set; }
}

public class Description
{
    public List<UpdatedProject> UpdatedProjects { get; set; }
}

public class UpdatedProject
{
    public UpdatedProject()
    {
        
    }

    public UpdatedProject(string name, string updatedContent, List<Dependency> updatedDependencies)
    {
        Name = name;
        UpdatedContent = updatedContent;
        UpdatedDependencies = updatedDependencies;
    }
    
    public string Name { get; set; }
    
    public string UpdatedContent { get; set; }
    public List<Dependency> UpdatedDependencies { get; set; }
}

public class Dependency
{
    public Dependency()
    {
        
    }

    public Dependency(string name, string currentVersion, string newVersion)
    {
        Name = name;
        CurrentVersion = currentVersion;
        NewVersion = newVersion;
    }
    
    public string Name { get; set; }
    public string CurrentVersion { get; set; }
    public string NewVersion { get; set; }
}