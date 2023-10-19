namespace sharp_dependency;

public static class DirectoryBuildPropsLookup
{
    public static string? GetDirectoryBuildPropsPath(IReadOnlyCollection<string> repositoryPaths, string projectPath, string basePath)
    {
        var directoryBuildPropsFiles = new List<string>();
        foreach (var repositoryPath in repositoryPaths)
        {
            if (!repositoryPath.EndsWith("Directory.Build.props") && !repositoryPath.EndsWith("Directory.build.props"))
            {
                continue;
            }
            
            directoryBuildPropsFiles.Add(repositoryPath);
        }
        
        if (!directoryBuildPropsFiles.Any())
        {
            return null;
        }

        string? levelToSearchOn = projectPath;
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


        var directoryBuildPropsUpper = directoryBuildPropsFiles.SingleOrDefault(x => x.Equals(Path.Combine(basePath, "Directory.Build.props")));
        if (directoryBuildPropsUpper is not null)
        {
            return directoryBuildPropsUpper;
        }
        
        var directoryBuildPropsLower = directoryBuildPropsFiles.SingleOrDefault(x => x.Equals(Path.Combine(basePath, "Directory.build.props")));
        return directoryBuildPropsLower;
    }
    
    public static string? GetDirectoryBuildPropsPath(IReadOnlyCollection<string> repositoryPaths, string projectPath)
    {
        var directoryBuildPropsFiles = new List<string>();
        foreach (var repositoryPath in repositoryPaths)
        {
            if (!repositoryPath.EndsWith("Directory.Build.props") && !repositoryPath.EndsWith("Directory.build.props"))
            {
                continue;
            }

            directoryBuildPropsFiles.Add(repositoryPath);
        }
        
        if (!directoryBuildPropsFiles.Any())
        {
            return null;
        }

        string? levelToSearchOn = projectPath;
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


        if (directoryBuildPropsFiles.Contains("Directory.Build.props"))
        {
            return "Directory.Build.props";
        }
        
        if (directoryBuildPropsFiles.Contains("Directory.build.props"))
        {
            return "Directory.build.props";
        }

        return null;
    }
}