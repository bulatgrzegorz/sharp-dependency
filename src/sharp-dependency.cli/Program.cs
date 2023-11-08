using sharp_dependency.cli;
using sharp_dependency.cli.Bitbucket;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
#if DEBUG
    config.PropagateExceptions();
#endif
    
    config.UseStrictParsing();
    config.AddBranch("config", x =>
    {
        x.AddCommand<ConfigureNugetCommand>("nuget").WithDescription("Create nuget configuration.");
        
        x.AddCommand<ConfigureCurrentContextCommand>("context").WithDescription("Create current context configuration.");
        x.AddCommand<PrintConfigurationCommand>("get").WithDescription("Print current configuration.");

        x.AddBranch("bitbucket", b =>
        {
            b.AddCommand<CreateBitbucketSourceCommand>("create").WithDescription("Create bitbucket source configuration.");
            b.AddCommand<DeleteBitbucketSourceCommand>("delete").WithDescription("Delete bitbucket source configuration.");
        });
    });
    
    //TODO: Parameters - include pre-release versions, way to limit updating only to specified packages (on specific version?)
    config.AddBranch("update", x =>
    {
        x.AddCommand<UpdateLocalDependencyCommand>("local").WithDescription("Update dependencies within local project.");
        x.AddCommand<UpdateRepositoryDependencyCommand>("repo").WithDescription("Update dependencies within remote repository.");
    });

    config.AddBranch("list", x =>
    {
        x.AddCommand<ListLocalDependencyCommand>("local").WithDescription("List dependencies within local project.");
    });
    //TODO: Create command for exploring dependencies (it could be useful to browse versions in entire workspace/project)
});

await app.RunAsync(args);