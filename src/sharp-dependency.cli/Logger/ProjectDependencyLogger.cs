﻿using sharp_dependency.Logger;
using Spectre.Console;

namespace sharp_dependency.cli.Logger;

public class ProjectDependencyLogger
{
    private Table? _table;
    private Tree? _tree;
    
    public void LogProject(string project)
    {
        _tree = new Tree(project);
        
        _table = new Table();
        _table.AddColumn("Dependency name");
        _table.AddColumn("Version");

        _tree.AddNode(_table);
    }

    public void LogDependency(string dependencyName, string version)
    {
        _table?.AddRow(dependencyName, version);
    }

    public void Flush()
    {
        if (_tree is not null && _table is not null)
        {
            AnsiConsole.Write(_tree);    
        }
    }
}