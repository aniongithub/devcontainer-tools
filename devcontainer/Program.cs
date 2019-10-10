using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using CliWrap;

namespace devcontainer
{
    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<InitOptions, ActivateOptions, LSOptions>(args)
                .WithParsed<InitOptions>(opts => Init(opts))
                .WithParsed<ActivateOptions>(opts => Activate(opts))
                .WithParsed<LSOptions>(opts => List(opts))
                .WithNotParsed(err => Console.Error.WriteLine($"{string.Join(',', err)}"));
        }

        static void Init(InitOptions opts)
        {
            var customVars = new Dictionary<string, string> {
                { "NAME", opts.Name ?? opts.TemplateName  },
                { "ID", opts.Id },
                { "DEV_DOCKERFILE", opts.DevDockerfile },
                { "DOCKERFILE", opts.Dockerfile },
                { "CONTEXT", opts.Context },
                { "SHUTDOWN_ACTION", opts.ShutdownAction },
                { "SHELL", opts.Shell },
                { "WORKSPACE_ROOT", opts.WorkspaceRoot }
            };

            // Set Name to TemplateName if not set
            if (opts.Name == Defaults.Name)
            {
                opts.Name = opts.TemplateName;
                customVars["NAME"] = opts.TemplateName;
            }
            
            // Find the source template
            var sourceTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Defaults.TemplatesPath, opts.TemplateName);
            if (!Directory.Exists(sourceTemplatePath))
            {
                Console.Error.WriteLine($"Could not find template {opts.TemplateName}!");
                Environment.Exit(1);
            }

            // Create blank Dockerfile if needed
            if (!File.Exists(Path.Combine(opts.Context, opts.Dockerfile)))
            {
                Console.WriteLine($"No Dockerfile was found in \"{opts.Context}\", creating blank Dockerfile....");
                File.Copy(Path.Combine(Defaults.DefaultTemplatePath, Defaults.Dockerfile), Path.Combine(opts.Context, opts.Dockerfile));
            }

            // Create .devcontainer folder
            var devContainerFolder = Path.Combine(opts.Context, Defaults.DevContainerFolder);
            if (!Directory.Exists(devContainerFolder))
                Directory.CreateDirectory(devContainerFolder);

            // Create our destination template folder
            var destTemplatePath = Path.Combine(devContainerFolder, opts.Name);

            // Copy to our destination path
            // Process variables, but pass through unknowns
            sourceTemplatePath.CopyTo(destTemplatePath, overwrite: opts.Overwrite,
                onCopyFile: (src, dst) => customVars.Process(src, dst, opts.Overwrite, passthroughUnknowns: true));
        }

        static void Activate(ActivateOptions opts)
        {
            var sourceTemplatePath = Path.Combine(Environment.CurrentDirectory, Defaults.DevContainerFolder, opts.Name);
            if (!Directory.Exists(sourceTemplatePath))
            {
                Console.Error.WriteLine($"Cannot find saved devcontainer with name {opts.Name}");
                Environment.Exit(1);
            }
            var destPath = Path.Combine(sourceTemplatePath, "..");


            var customVars = new Dictionary<string, string> {
                { "USER_UID", Cli.Wrap("id").SetArguments("-u").Execute().StandardOutput.Trim('\r','\n', ' ') },
                { "USER_GID", Cli.Wrap("id").SetArguments("-g").Execute().StandardOutput.Trim('\r','\n', ' ') }
            };

            // This time, merge custom vars with the environment
            // Environment vars overwrite any custom vars
            var mergedEnv = Environment
                .GetEnvironmentVariables()
                .ToReadOnlyDictionary()
                .MergeWithUpdates(customVars);

            // Substitute vars with environment values and activate as current devcontainer
            sourceTemplatePath.CopyTo(destPath,
                onCopyFile: (src, dst) => mergedEnv.Process(src, dst, opts.DiscardChanges, passthroughUnknowns: true));
        }

        static void List(LSOptions opts)
        {
            var devcontainerFolder = Path.Combine(opts.Context, Defaults.DevContainerFolder);
            if (!Directory.Exists(devcontainerFolder))
            {
                Console.WriteLine("No saved devcontainers!");
                Environment.Exit(0);
            }

            foreach (var subdir in Directory.GetDirectories(devcontainerFolder))
                Console.WriteLine(Path.GetFileName(subdir));
        }

        static void Start(StartOptions opts)
        {

        }
        static void Stop(StopOptions opts)
        {

        }
        static void Connect(ConnectOptions opts)
        {

        }
    }
}