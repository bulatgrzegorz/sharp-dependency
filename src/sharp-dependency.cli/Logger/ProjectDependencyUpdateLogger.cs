using sharp_dependency.Logger;
using Spectre.Console;

namespace sharp_dependency.cli.Logger;

public class ProjectDependencyUpdateLogger : IProjectDependencyUpdateLogger
{
    private Table? _table;
    private Tree? _tree;
    
    public void LogProject(string project)
    {
        var path = new TextPath(project)
            .RootStyle(new Style(foreground: Color.Red))
            .SeparatorStyle(new Style(foreground: Color.Green))
            .StemStyle(new Style(foreground: Color.Blue))
            .LeafStyle(new Style(foreground: Color.Yellow));
        
        _tree = new Tree(path);
        
        _table = new Table();
        _table.AddColumn("Dependency name");
        _table.AddColumn("Current version");
        _table.AddColumn("New version");

        _tree.AddNode(_table);
    }

    public void LogDependency(string dependencyName, string currentVersion, string newVersion)
    {
        _table?.AddRow(dependencyName, currentVersion, newVersion);
    }

    public void Flush()
    {
        if (_tree is not null && _table is {Columns.Count: > 0})
        {
            AnsiConsole.Write(_tree);    
        }
    }
}