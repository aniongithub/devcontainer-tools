using System;
using CommandLine;

using devcontainer.core;

namespace devcontainer
{
    [Verb("init", HelpText = "Initialize a devcontainer from a template or custom set of options")]
    public sealed class InitOptions: IInitOptions
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

        [Option("disable-hooks", Default = false, HelpText = "Don't run any hooks when activating the template")]
        public bool DisableHooks { get; set; }

        public InitOptions()
        {
            Id = Guid.NewGuid().ToString().Replace("-", string.Empty);
        }
    }

    [Verb("activate", HelpText = "Activate the specified devcontainer files (devcontainer rebuild may be necessary)")]
    public sealed class ActivateOptions: IActivateOptions
    {
        [Value(0, HelpText = "Name of the saved devcontainer to activate")]
        public string Name { get; set; }

        [Option('o', "overwrite", Default = false, HelpText = "Overwrite any files being used by the current devcontainer")]
        public bool Overwrite { get; set; }

        [Option("disable-hooks", Default = false, HelpText = "Don't run any hooks when activating the template")]
        public bool DisableHooks { get; set; }
    }

    [Verb("deactivate", HelpText = "Deactivate the active devcontainer")]
    public sealed class DeactivateOptions: IDeactivateOptions
    {
        [Option("disable-hooks", Default = false, HelpText = "Don't run any hooks when deactivating the template")]
        public bool DisableHooks { get; set; }
    }

    [Verb("ls", HelpText = "List all available devcontainers")]
    public sealed class LSOptions: ILSOptions
    {
        [Option(Default = Defaults.Context,
            HelpText = "The context to search for saved devcontainers in")]
        public string Context { get; set; }
    }

    [Verb("run", HelpText = "Run a one-off command in a new instance of the active devcontainer")]
    public sealed class RunOptions: IRunOptions
    {
        [Option(Default = Defaults.Context, HelpText = "The context to search for saved devcontainers in")]
        public string Context { get; set; }

        [Value(0, HelpText = "Command to run within the container")]
        public string Command { get; set; }

        [Option('w', "workdir", Default = "", HelpText = "The working directory within the container")]
        public string WorkDir { get; set; }
    }    

    [Verb("start", HelpText = "Start active devcontainer")]
    public sealed class StartOptions: IStartOptions
    {
        [Option(Default = Defaults.Context,
            HelpText = "The context to search for saved devcontainers in")]
        public string Context { get; set; }

        [Option("build", Default = true,
            HelpText = "(Re)build Docker images if necessary")]
        public bool Build { get; set; }        
    }
    
    [Verb("stop", HelpText = "Stop active devcontainer")]
    public sealed class StopOptions: IStopOptions
    {
        [Option(Default = Defaults.Context,
            HelpText = "The context to search for saved devcontainers in")]
        public string Context { get; set; }
        [Option("timeout", Default = 10, HelpText = "Specify a shutdown timeout in seconds")]
        public int TimeoutSec { get; set; }
    }
    
    [Verb("status", HelpText = "Show status of the active devcontainer")]
    public sealed class StatusOptions: IStatusOptions
    {
        [Option(Default = Defaults.Context,
            HelpText = "The context to search for saved devcontainers in")]
        public string Context { get; set; }
    }
    
    [Verb("exec", HelpText = "Execute a command in an existing instance of the active devcontainer")]
    public sealed class ExecOptions: IExecOptions
    {
        [Option(Default = Defaults.Context,
            HelpText = "The context to search for saved devcontainers in")]
        public string Context { get; set; }

        [Value(0, HelpText = "Command to run within the container")]
        public string Command { get; set; }

        [Option('w', "workdir", Default = "", HelpText = "The working directory within the container")]
        public string WorkDir { get; set; }
    }
}