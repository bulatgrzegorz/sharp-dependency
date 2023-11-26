using sharp_dependency.Parsers;
using sharp_dependency.Repositories;
using Spectre.Console.Cli;

namespace sharp_dependency.cli.DependencyCommands;

public abstract class RepositoryDependencyCommandBase<T> : AsyncCommand<T> where T : CommandSettings
{
    protected async Task<string> GetProjectContent(IRepositoryManger repositoryManger, string projectPath)
    {
        return await repositoryManger.GetFileContentRaw(projectPath);
    }
    
    protected async Task<string?> GetDirectoryBuildPropsContent(IRepositoryManger repositoryManger, IReadOnlyCollection<string> repositoryPaths, string projectPath)
    {
        var directoryBuildPropsFile = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(repositoryPaths, projectPath);
        return directoryBuildPropsFile is not null ? await repositoryManger.GetFileContentRaw(directoryBuildPropsFile) : null;
    }
    
    protected async Task<IReadOnlyCollection<string>> GetProjectPaths(IReadOnlyCollection<string> filePaths, IRepositoryManger bitbucketServerManager)
    {
        var solutionsPath = FindSolutionPath(filePaths);
        if (solutionsPath is null) return filePaths.Where(FileIsProjectFile).ToList();
        
        var solutionParser = new SolutionFileParser();
        var solutionContent = await bitbucketServerManager.GetFileContent(solutionsPath);
        // Paths in sln file are using backslash while everywhere else (url, ...) we are going to use forward slash
        return solutionParser.GetProjectPaths(solutionContent).Select(x => x.Replace("\\", "/")).ToList();
    }

    private string? FindSolutionPath(IReadOnlyCollection<string> filePaths)
    {
        var solutionsPaths = FindSolutionFiles(filePaths);
        if (solutionsPaths.Count == 0)
        {
            return null;
        }

        return FindSolutionPathSrcLevel(solutionsPaths) ?? FindSolutionPathRootLevel(solutionsPaths) ?? solutionsPaths.First();
    }

    private IReadOnlyCollection<string> FindSolutionFiles(IReadOnlyCollection<string> filePaths)
    {
        return filePaths.Where(FileIsSln).ToList();
    }

    private string? FindSolutionPathRootLevel(IReadOnlyCollection<string> solutionPaths)
    {
        return solutionPaths.FirstOrDefault(DirectoryIsEmpty);
    }
    
    private string? FindSolutionPathSrcLevel(IReadOnlyCollection<string> solutionPaths)
    {
        return solutionPaths.FirstOrDefault(DirectoryIsSrc);
    }

    private static bool DirectoryIsEmpty(string path) => string.IsNullOrEmpty(Path.GetDirectoryName(path));
    private static bool DirectoryIsSrc(string path) => Path.GetDirectoryName(path)?.Equals("src", StringComparison.InvariantCultureIgnoreCase) ?? false;
    private static bool FileIsProjectFile(string path) => path.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase);
    private static bool FileIsSln(string path) => path.EndsWith(".sln", StringComparison.InvariantCultureIgnoreCase);
}