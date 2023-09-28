﻿using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NuGet.Versioning;

//TODO: Some configuration file, where it will be possible to configure which dependencies and how should be updated
//TODO: CLI tool - update remote repository (with PR)
//TODO: Implementations as Bitbucket should be more generic, allowing user to configure many of them with auth method
//TODO: Support directory.build.props files

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

interface IRepositoryManger
{
    Task<IEnumerable<string>> GetRepositoryFilePaths();
    Task<FileContent> GetFileContent(string filePath);
    Task<Commit> EditFile(string branch, string commitMessage, string content, string filePath);
    Task CreatePullRequest(string sourceBranch, string targetBranch, string prName, string description);
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