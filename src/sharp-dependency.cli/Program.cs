using sharp_dependency.cli.ConfigCommands;
using sharp_dependency.cli.ConfigCommands.Bitbucket;
using sharp_dependency.cli.DependencyCommands;
using sharp_dependency.cli.Logger;
using sharp_dependency.Logger;
using Spectre.Console.Cli;

#if DEBUG
Log.Logger = new AnsiConsoleLogger(AnsiConsoleLogger.LogLevel.Debug);
#else
Log.Logger = new AnsiConsoleLogger(AnsiConsoleLogger.LogLevel.Info);
#endif

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
        
        //TODO: Add default bitbucket settings (workspace/project)
        x.AddCommand<ConfigureCurrentContextCommand>("context").WithDescription("Create current context configuration.");
        x.AddCommand<PrintConfigurationCommand>("get").WithDescription("Print current configuration.");

        x.AddBranch("bitbucket", b =>
        {
            b.AddCommand<CreateBitbucketSourceCommand>("create").WithDescription("Create bitbucket source configuration.");
            b.AddCommand<DeleteBitbucketSourceCommand>("delete").WithDescription("Delete bitbucket source configuration.");
        });
    });
    
    config.AddBranch("update", x =>
    {
        x.AddCommand<UpdateLocalDependencyCommand>("local").WithDescription("Update dependencies within local project.");
        x.AddCommand<UpdateRepositoryDependencyCommand>("repo").WithDescription("Update dependencies within remote repository.");
    });

    config.AddBranch("migrate", x =>
    {
        x.AddCommand<MigrateLocalDependencyCommand>("local")
            .WithDescription("Migrate dependencies within local project")
            .WithExample("migrate", "local", "-u \"package.name:[1.1.0,2.0.0)\"");

        x.AddCommand<MigrateRepositoryDependencyCommand>("repo")
            .WithDescription("Migrate dependencies within remote repository");
    });

    config.AddBranch("list", x =>
    {
        x.AddCommand<ListLocalDependencyCommand>("local").WithDescription("List dependencies within local project.");
        x.AddCommand<ListRepositoryDependencyCommand>("repo").WithDescription("List dependencies within remote repository.");
    });
});



await app.RunAsync(args);