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
    public List<Project> UpdatedProjects { get; set; }
}

public class Project
{
    public string Name { get; set; }
    
    public List<Dependency> UpdatedDependencies { get; set; }
}

public class Dependency
{
    public string Name { get; set; }
    public string CurrentVersion { get; set; }
    public string NewVersion { get; set; }
}