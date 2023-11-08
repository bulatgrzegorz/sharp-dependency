namespace sharp_dependency;

//TODO: Right now we do not support nested directory build props files. First one will be chosen (in terms of directory distance)
public static class DirectoryBuildPropsLookup
{
    private const string DirectoryBuildPropsUpper = "Directory.Build.props";
    private const string DirectoryBuildPropsLower = "Directory.build.props";

    public static IReadOnlyCollection<string> SearchForDirectoryBuildPropsFiles(string path, bool recursive)
    {
        var searchOptions = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var upperBuildProps = Directory.GetFiles(path, DirectoryBuildPropsUpper, searchOptions);
        var lowerBuildProps = Directory.GetFiles(path, DirectoryBuildPropsLower, searchOptions);
        
        return upperBuildProps.Concat(lowerBuildProps).ToList();
    }

    public static string? GetDirectoryBuildPropsPath(IReadOnlyCollection<string> directoryBuildPropsFiles, string projectPath, string basePath)
    {
        //This list should contains already only directory build props files, but we will call filter for double check
        var filteredDirectoryBuildPropsFiles = FilterDirectoryBuildPropsFiles(directoryBuildPropsFiles).ToList();
        
        if (!filteredDirectoryBuildPropsFiles.Any())
        {
            return null;
        }

        var levelToSearchOn = projectPath;
        do
        {
            levelToSearchOn = Path.GetDirectoryName(levelToSearchOn);
            foreach (var path in filteredDirectoryBuildPropsFiles)
            {
                if (Path.GetDirectoryName(path) != levelToSearchOn)
                {
                    continue;
                }

                return path;
            }
        } 
        while (levelToSearchOn != basePath);


        var directoryBuildPropsUpper = filteredDirectoryBuildPropsFiles.SingleOrDefault(x => x.Equals(Path.Combine(basePath, DirectoryBuildPropsUpper)));
        if (directoryBuildPropsUpper is not null)
        {
            return directoryBuildPropsUpper;
        }
        
        var directoryBuildPropsLower = filteredDirectoryBuildPropsFiles.SingleOrDefault(x => x.Equals(Path.Combine(basePath, DirectoryBuildPropsLower)));
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