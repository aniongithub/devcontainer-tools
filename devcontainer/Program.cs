using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using CommandLine;

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
                { "SHELL", opts.Shell }
            };
            // Set Name to TemplateName if not set
            if (opts.Name == Defaults.Name)
            {
                opts.Name = opts.TemplateName;
                customVars["NAME"] = opts.TemplateName;
            }
            
            // Merge custom values, allow the host environment to overwrite values
            var mergedEnv = Environment
                .GetEnvironmentVariables()
                .ToReadOnlyDictionary()
                .MergeWithUpdates(customVars);

            var sourceTemplatePath = Path.Combine(Defaults.TemplatesPath, opts.TemplateName);
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

            // Create our template folder
            var destTemplatePath = Path.Combine(devContainerFolder, opts.Name);

            // Process docker-compose.yml
            mergedEnv.Process(
                Path.Combine(sourceTemplatePath, Defaults.DockerComposeFile), 
                Path.Combine(destTemplatePath, Defaults.DockerComposeFile));

            // Process devcontainer.json
            mergedEnv.Process(
                Path.Combine(sourceTemplatePath, Defaults.DevcontainerJsonFile),
                Path.Combine(destTemplatePath, Defaults.DevcontainerJsonFile));

            // Process dev.Dockerfile
            mergedEnv.Process(
                Path.Combine(sourceTemplatePath, Defaults.DevDockerfile),
                Path.Combine(destTemplatePath, Defaults.DevDockerfile));
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
            sourceTemplatePath.CopyTo(destPath, overwrite: opts.DiscardChanges);
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