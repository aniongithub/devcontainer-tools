using System;
using CommandLine;

namespace devcontainer
{
    public static class Defaults
    {
        public const string Dockerfile = "Dockerfile";
        public const string DevDockerfile = "dev.Dockerfile";
        public const string DevContainerFolder = ".devcontainer";
        public const string DockerComposeFile = "docker-compose.yml";
        public const string DevcontainerJsonFile = "devcontainer.json";
        public const string Name = null;
        public const string ShutdownAction = "stopCompose";
        public const string Shell = "/bin/bash";

        public const string TemplateName = "default";
        public const string Context = ".";

        public const string TemplatesPath = "templates/";
        public const string Template = "default";

        public const string DefaultTemplatePath = TemplatesPath + Template;
        public const string WorkspaceRoot = ".";
        public const string ConfigDir = ".devcontainer";

        public const string DevContainerEnvFilename = "devcontainer.env";
        public const string DevContainerJsonFilename = "devcontainer.json";
    }

    [Verb("init", HelpText = "Initialize a devcontainer from a template or custom set of options")]
    public sealed class InitOptions
    {
        [Value(0, Required = false, Default = Defaults.TemplateName, HelpText = "The id of the template to use")]
        public string TemplateName { get; set;}
        
        [Value(1, Default = Defaults.Context,
            HelpText = "The directory to use as the devcontainer build context")]
        public string Context { get; set; }

        [Option('n', "name", Default = Defaults.Name)]
        public string Name { get; set; }

        [Option("shutdown-action", Default = Defaults.ShutdownAction,
            HelpText = "The action to perform when the root shell exits")]
        public string ShutdownAction { get; set; }

        [Option('s', "shell", Default = Defaults.Shell,
            HelpText = "The default shell to start")]
        public string Shell { get; set; }

        [Option(Hidden = true, Required = false)]
        public string Id { get; set; }

        [Option('d', "dockerfile", Required = false, Default = Defaults.Dockerfile,
            HelpText = "The Dockerfile to use as a base for the devcontainer")]
        public string Dockerfile { get; set; }

        [Option(Hidden = true, Required = false, Default = Defaults.DevDockerfile,
            HelpText = "The devcontainer Dockerfile")]
        public string DevDockerfile { get; set; }

        [Option('o', "overwrite", Default = false, HelpText = "Overwrite any existing files")]
        public bool Overwrite { get; set; }

        [Option('w', "workspace-root", Default = Defaults.WorkspaceRoot )]
        public string WorkspaceRoot { get; set; }

        public InitOptions()
        {
            Id = Guid.NewGuid().ToString().Replace("-", string.Empty);
        }
    }

    [Verb("activate", HelpText = "Activate the specified devcontainer files (devcontainer rebuild may be necessary)")]
    public sealed class ActivateOptions
    {
        [Value(0, HelpText = "Name of the saved devcontainer to activate")]
        public string Name { get; set; }

        [Option('d', "discard-changes", Default = false, HelpText = "Discard any changes to files in the root devcontainer folder during activation")]
        public bool DiscardChanges { get; set; }
    }

    [Verb("ls", HelpText = "List all available devcontainers")]
    public sealed class LSOptions
    {
        [Option(Default = Defaults.Context,
            HelpText = "The context to search for saved devcontainers in")]
        public string Context { get; set; }
    }

    [Verb("start", HelpText = "Start a devcontainer")]
    public sealed class StartOptions
    {

    }
    
    [Verb("stop", HelpText = "Stop a devcontainer")]
    public sealed class StopOptions
    {

    }
    
    [Verb("connect", HelpText = "Connect to a running devcontainer")]
    public sealed class ConnectOptions
    {

    }

    [Verb("run", HelpText = "Run one more commands in a devcontainer")]
    public sealed class RunOptions
    {
    }
    
    [Verb("ps", HelpText = "List all running devcontainers")]
    public sealed class PSOptions
    {
    }
    

    [Verb("rm", HelpText = "Remove a devcontainer configuration")]
    public sealed class RMOptions
    {
    }
}