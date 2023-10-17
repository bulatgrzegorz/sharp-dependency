# sharp-dependency

Sharp-dependency is a tool that make updating dependency in your .NET projects easier or even totaly automatic. Inspiration for creation was excellent [dependabot](https://github.com/dependabot) and [dotnet-outdated](https://github.com/dotnet-outdated/dotnet-outdated). 

## Motivation

Although project was mainly ment as way for personal growth there was also few features/behaviours missing in each of previously mentioned tools.

## Installation

Sharp-dependency is avaiable on nuget: [https://www.nuget.org/packages/sharpoogle/](https://www.nuget.org/packages/sharp-dependency-tool) as dotnet tool.
You can install it by simply running:
```cmd
dotnet tool install --global sharp-dependency-tool
```

to update existing version:
```cmd
dotnet tool update --global sharp-dependency-tool
```

> **Warning** 
> For now only prerelease version is avaiable, use `--prerelease` flag in installation command.

## Usage

After installing you can run:
```cmd
sharp-dependency -h
```
in order to get command's help:
```cmd
USAGE:
    sharp-dependency.cli.dll [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help       Prints help information
    -v, --version    Prints version information

COMMANDS:
    config
    update
```

### Update command

Update command contain two subcommands.

#### Local

Command can be executed both with or without path parameter. If no path was given, sharp-dependency will search through current directory and try to find solution file or project files (in that order).
```cmd
DESCRIPTION:
Update dependencies within local project.

USAGE:
    sharp-dependency.cli.dll update local [path] [OPTIONS]

ARGUMENTS:
    [path]    Path to solution/csproj which dependency should be updated

OPTIONS:
    -h, --help       Prints help information
        --dry-run    Command will determine dependencies to be updated without actually updating them
```

#### Repo


