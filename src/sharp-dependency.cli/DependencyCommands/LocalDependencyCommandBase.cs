using sharp_dependency.Parsers;
using sharp_dependency.Repositories;
using Spectre.Console.Cli;

namespace sharp_dependency.cli.DependencyCommands;

public abstract class LocalDependencyCommandBase<T> : AsyncCommand<T> where T : CommandSettings
{
    private bool IsPathSolutionFile(string path) => path.EndsWith(".sln", StringComparison.InvariantCultureIgnoreCase);
    private bool IsPathProjectFile(string path) => path.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase);
    
    protected (string basePath, IReadOnlyCollection<string> projectPaths, IReadOnlyCollection<string> directoryBuildProps) GetRepositoryFiles(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            var projects = GetProjectFilesForCurrentDirectory();
            return (Directory.GetCurrentDirectory(), projects, DirectoryBuildPropsLookup.SearchForDirectoryBuildPropsFiles(Directory.GetCurrentDirectory(), true));
        }

        var basePath = Path.GetDirectoryName(path)!;
        if (IsPathSolutionFile(path))
        {
            var solutionFileParser = new SolutionFileParser();
            var pathProjects = solutionFileParser.GetProjectPaths(FileContent.CreateFromLocalPath(path));
        
            return (basePath, pathProjects, DirectoryBuildPropsLookup.SearchForDirectoryBuildPropsFiles(basePath, true));
        }

        if (IsPathProjectFile(path))
        {
            return (basePath, new[] { path }, DirectoryBuildPropsLookup.SearchForDirectoryBuildPropsFiles(basePath, false));
        }

        if (!File.GetAttributes(path).HasFlag(FileAttributes.Directory))
        {
            throw new ArgumentException($"Only solution and project file are supported for now. Got path: {path}");
        }
        
        return (path, GetProjectFiles(path), DirectoryBuildPropsLookup.SearchForDirectoryBuildPropsFiles(path, true));
    }
    
    private static IReadOnlyCollection<string> GetProjectFilesForCurrentDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        return GetProjectFiles(currentDirectory);
    }

    private static IReadOnlyCollection<string> GetProjectFiles(string directory)
    {
        var solutionFiles = Directory.GetFiles(directory, "*.sln");
        if (solutionFiles.Length > 1)
        {
            throw new ArgumentException(
                $"There are multiple solution files ({string.Join(", ", solutionFiles)}) in current directory ({directory}). Please pass specific file (sln/csproj) that should be updated.");
        }

        if (solutionFiles.Length == 1)
        {
            var solutionFileParser = new SolutionFileParser();
            return solutionFileParser.GetProjectPaths(FileContent.CreateFromLocalPath(solutionFiles[0]));
        }

        var projectFiles = Directory.GetFiles(directory, "*.csproj");
        if (projectFiles.Length == 0)
        {
            Console.WriteLine(
                $"Could not find any project file in current directory ({directory}). Please either change directory or pass specific file (sln/csproj) that should be updated.");
        }

        return projectFiles;
    }
}