using sharp_dependency.cli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.PropagateExceptions();
    config.UseStrictParsing();
    config.AddBranch("config", x =>
    {
        x.AddCommand<ConfigureNugetCommand>("nuget");
        x.AddCommand<ConfigureBitbucketCommand>("bitbucket");
        //TODO: Add show command - tokens and passwords should be hidden 
        //TODO: Add command to remove bitbucket source by name
    });
    config.AddBranch("update", x =>
    {
        x.AddCommand<UpdateLocalDependencyCommand>("local");
        x.AddCommand<UpdateRepositoryDependencyCommand>("repo");
    });
});

await app.RunAsync(args);