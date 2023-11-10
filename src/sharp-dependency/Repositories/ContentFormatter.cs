using System.Text;

namespace sharp_dependency.Repositories;

public class ContentFormatter
{
    public static string FormatPullRequestDescription(Description description)
    {
        if (description.UpdatedProjects is null or {Count: 0})
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder();
        foreach (var project in description.UpdatedProjects.Where(project => project.UpdatedDependencies is { Count: > 0 }))
        {
            stringBuilder.Append($"* {project.Name}\n");
            foreach (var dependency in project.UpdatedDependencies)
            {
                stringBuilder.Append($"    * {dependency.Name} {dependency.CurrentVersion} -> {dependency.NewVersion}\n");
            }
        }

        stringBuilder.Length -= 1;
        return stringBuilder.ToString();
    }
}