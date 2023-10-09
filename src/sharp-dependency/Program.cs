using System.Diagnostics.CodeAnalysis;
using NuGet.Versioning;

//TODO: Some configuration file, where it will be possible to configure which dependencies and how should be updated
//TODO: CLI tool - update remote repository (with PR)
//TODO: Support directory.build.props files
//TODO: Support target framework conditions in csproj item groups

public class Dependency
{
    public Dependency(string name, string currentVersion, Action<string> updateVersionMethod)
    {
        Name = name;
        CurrentVersion = currentVersion;
        UpdateVersionMethod = updateVersionMethod;
        CurrentNugetVersion = NuGetVersion.Parse(CurrentVersion);
        VersionRange = new VersionRange(CurrentNugetVersion, new FloatRange(NuGetVersionFloatBehavior.Major, CurrentNugetVersion));
    }

    public bool UpdateVersionIfPossible(IReadOnlyCollection<NuGetVersion> allVersions, [NotNullWhen(true)] out NuGetVersion? newVersion)
    {
        newVersion = null;
        var versionToUpdate = VersionRange.FindBestMatch(allVersions);
        if (versionToUpdate is null)
        {
            return false;
        }

        if (!VersionRange.IsBetter(CurrentNugetVersion, versionToUpdate))
        {
            return false;
        }

        newVersion = versionToUpdate;
        UpdateVersionMethod(versionToUpdate.ToNormalizedString());

        return true;
    }

    public string Name { get; }
    public string CurrentVersion { get; }
    private Action<string> UpdateVersionMethod { get; }
    private VersionRange VersionRange { get; }
    private NuGetVersion CurrentNugetVersion { get; }
}

interface IFileParser
{
    Task<ProjectFile> Parse();
    Task<string> Generate();
}

public interface IRepositoryManger
{
    Task<IEnumerable<string>> GetRepositoryFilePaths();
    Task<FileContent> GetFileContent(string filePath);
    Task<string> GetFileContentRaw(string filePath);
    Task<Commit> CreateCommit(string branch, string commitMessage, List<(string filePath, string content)> files);
    Task<PullRequest> CreatePullRequest(string sourceBranch, string targetBranch, string name, string description);
    Task<PullRequest> CreatePullRequest(string sourceBranch, string name, string description);
}

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

public class PullRequest
{
    public int Id { get; set; }
}

public class FileContent
{
    public FileContent(IEnumerable<string> lines, string path)
    {
        Lines = lines;
        Path = path;
    }

    public static FileContent CreateFromLocalPath(string path)
    {
        return new FileContent(File.ReadAllLines(path), path);
    }
    
    public IEnumerable<string> Lines { get; private set; }
    public string Path { get; private set; }
}

public class Commit
{
    public string Id { get; set; }
}