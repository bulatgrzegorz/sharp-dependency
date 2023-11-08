namespace sharp_dependency.Logger;

public interface IProjectDependencyUpdateLogger
{
    void LogProject(string project);
    void LogDependency(string dependencyName, string currentVersion, string newVersion);
    void Flush();
}