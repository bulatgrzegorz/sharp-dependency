using sharp_dependency.Logger;

namespace sharp_dependency.IntegrationTests;

public class DummyLogger : IProjectDependencyUpdateLogger
{
    public void LogProject(string project)
    {
    }

    public void LogDependency(string dependencyName, string currentVersion, string newVersion)
    {
    }

    public void Flush()
    {
    }
}