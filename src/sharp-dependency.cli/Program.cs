using sharp_dependency.cli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.PropagateExceptions();
    config.UseStrictParsing();
    config.AddBranch("config", x =>
    {
        x.AddCommand<ConfigureNugetCommand>("nuget").WithDescription("Create nuget configuration.");
        x.AddCommand<ConfigureBitbucketCommand>("bitbucket").WithDescription("Create bitbucket configuration.");
        x.AddCommand<ConfigureCurrentContextCommand>("context").WithDescription("Create current context configuration.");
        x.AddCommand<PrintConfigurationCommand>("get").WithDescription("Print current configuration.");
        //TODO: Add command to remove bitbucket source by name
    });
    //TODO: Parameters - include pre-release versions, way to limit updating only to specified packages (on specific version?)
    config.AddBranch("update", x =>
    {
        x.AddCommand<UpdateLocalDependencyCommand>("local").WithDescription("Update dependencies within local project.");
        x.AddCommand<UpdateRepositoryDependencyCommand>("repo").WithDescription("Update dependencies within remote repository.");
    });
    //TODO: Create command for exploring dependencies (it could be useful to browse versions in entire workspace/project)
});

await app.RunAsync(args);