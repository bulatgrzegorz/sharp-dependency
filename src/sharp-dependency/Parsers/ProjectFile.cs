namespace sharp_dependency.Parsers;

public class ProjectFile
{
    public ProjectFile(IReadOnlyCollection<Dependency> dependencies, IReadOnlyCollection<string> targetFrameworks)
    {
        Dependencies = dependencies;
        TargetFrameworks = targetFrameworks;
    }

    public IReadOnlyCollection<Dependency> Dependencies { get; private set; }
    public IReadOnlyCollection<string> TargetFrameworks { get; private set; }
}