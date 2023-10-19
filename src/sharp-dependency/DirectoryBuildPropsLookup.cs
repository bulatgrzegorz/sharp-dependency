namespace sharp_dependency;

public static class DirectoryBuildPropsLookup
{
    private const string DirectoryBuildPropsUpper = "Directory.Build.props";
    private const string DirectoryBuildPropsLower = "Directory.build.props";
    
    public static string? GetDirectoryBuildPropsPath(IReadOnlyCollection<string> repositoryPaths, string projectPath, string basePath)
    {
        var directoryBuildPropsFiles = FilterDirectoryBuildPropsFiles(repositoryPaths).ToList();
        
        if (!directoryBuildPropsFiles.Any())
        {
            return null;
        }

        var levelToSearchOn = projectPath;
        do
        {
            levelToSearchOn = Path.GetDirectoryName(levelToSearchOn);
            foreach (var path in directoryBuildPropsFiles)
            {
                if (Path.GetDirectoryName(path) != levelToSearchOn)
                {
                    continue;
                }

                return path;
            }
        } 
        while (levelToSearchOn != basePath);


        var directoryBuildPropsUpper = directoryBuildPropsFiles.SingleOrDefault(x => x.Equals(Path.Combine(basePath, DirectoryBuildPropsUpper)));
        if (directoryBuildPropsUpper is not null)
        {
            return directoryBuildPropsUpper;
        }
        
        var directoryBuildPropsLower = directoryBuildPropsFiles.SingleOrDefault(x => x.Equals(Path.Combine(basePath, DirectoryBuildPropsLower)));
        return directoryBuildPropsLower;
    }
    
    public static string? GetDirectoryBuildPropsPath(IReadOnlyCollection<string> repositoryPaths, string projectPath)
    {
        var directoryBuildPropsFiles = FilterDirectoryBuildPropsFiles(repositoryPaths).ToList();

        if (!directoryBuildPropsFiles.Any())
        {
            return null;
        }

        var levelToSearchOn = projectPath;
        do
        {
            levelToSearchOn = Path.GetDirectoryName(levelToSearchOn);
            foreach (var path in directoryBuildPropsFiles)
            {
                if (Path.GetDirectoryName(path) != levelToSearchOn)
                {
                    continue;
                }

                return path;
            }
        } 
        while (levelToSearchOn is not null && levelToSearchOn.Contains(Path.DirectorySeparatorChar));


        if (directoryBuildPropsFiles.Contains(DirectoryBuildPropsUpper))
        {
            return DirectoryBuildPropsUpper;
        }
        
        if (directoryBuildPropsFiles.Contains(DirectoryBuildPropsLower))
        {
            return DirectoryBuildPropsLower;
        }

        return null;
    }

    private static IEnumerable<string> FilterDirectoryBuildPropsFiles(IReadOnlyCollection<string> repositoryPaths)
    {
        foreach (var repositoryPath in repositoryPaths)
        {
            if (!repositoryPath.EndsWith(DirectoryBuildPropsUpper) && !repositoryPath.EndsWith(DirectoryBuildPropsLower))
            {
                continue;
            }

            yield return repositoryPath;
        }
    }
}