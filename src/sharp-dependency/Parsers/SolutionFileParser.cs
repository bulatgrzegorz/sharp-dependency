using System.Text.RegularExpressions;
using sharp_dependency.Repositories;

namespace sharp_dependency.Parsers;

public partial class SolutionFileParser
{
    public IReadOnlyCollection<string> GetProjectPaths(FileContent solutionFile)
    {
        var result = new List<string>();
        var solutionFileDirectory = Path.GetDirectoryName(solutionFile.Path);
        foreach (var line in solutionFile.Lines)
        {
            if (!SolutionProjectRegex().IsMatch(line)) continue;
            
            var relativeProjectPath = line.Split("\"")[5];
            if(!ProjectFileExtensionRegex().IsMatch(relativeProjectPath)) continue;
            
            result.Add(string.IsNullOrEmpty(solutionFileDirectory) ? relativeProjectPath : Path.Combine(solutionFileDirectory, relativeProjectPath));
        }
        
        return result;
    }

    [GeneratedRegex(@".+\.[a-z]{2}proj$")]
    private static partial Regex ProjectFileExtensionRegex();
    
    [GeneratedRegex(@"^\s*Project\(")]
    private static partial Regex SolutionProjectRegex();
}