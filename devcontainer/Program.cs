using Anotar.LibLog;
using CliWrap;
using CommandLine;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Newtonsoft.Json;

namespace devcontainer
{
    class Program
    {
        static void Main(string[] args)
        {
            Logging.LogProvider.SetCurrentLogProvider(new Logging.ColoredConsoleLogProvider());

            Parser.Default.ParseArguments<InitOptions, ActivateOptions, LSOptions>(args)
                .WithParsed<InitOptions>(opts => Init(opts))
                .WithParsed<ActivateOptions>(opts => Activate(opts))
                .WithParsed<LSOptions>(opts => List(opts))
                .WithNotParsed(err => Console.Error.WriteLine($"{string.Join(',', err)}"));
        }

        static void Init(InitOptions opts)
        {
            var customVars = new Dictionary<string, string> {
                { "DEVCONTAINER_NAME", opts.Name ?? opts.TemplateName  },
                { "DEVCONTAINER_ID", opts.Id },
                { "DEVCONTAINER_DEV_DOCKERFILE", opts.DevDockerfile },
                { "DEVCONTAINER_BASE_DOCKERFILE", opts.Dockerfile },
                { "DEVCONTAINER_CONTEXT", opts.Context },
                { "DEVCONTAINER_SHUTDOWN_ACTION", opts.ShutdownAction },
                { "DEVCONTAINER_SHELL", opts.Shell },
                { "DEVCONTAINER_WORKSPACE_ROOT", opts.WorkspaceRoot }
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
                LogTo.Error($"Could not find template {opts.TemplateName}!");
                Environment.Exit(1);
            }
            else
                LogTo.Info($"Found {opts.TemplateName}");

            // Create blank Dockerfile if needed
            if (!File.Exists(Path.Combine(opts.Context, opts.Dockerfile)))
            {
                LogTo.Warn($"No Dockerfile was found in \"{opts.Context}\", creating blank Dockerfile {opts.Dockerfile}. Please update this to install any prerequisites your build environment needs");
                File.Copy(Path.Combine(Defaults.DefaultTemplatePath, Defaults.Dockerfile), Path.Combine(opts.Context, opts.Dockerfile));
            }
            else
                LogTo.Info($"{opts.Dockerfile} found, will use to build base image");

            // Create .devcontainer folder
            var devContainerFolder = Path.Combine(opts.Context, Defaults.DevContainerFolder)
                .EnsureDirectoriesExist();

            // Create our destination template folder
            var destTemplatePath = Path.Combine(devContainerFolder, opts.Name)
                .EnsureDirectoriesExist();

            // Create devcontainer.env file
            var devContainerEnvFilename = Path.Combine(destTemplatePath, Defaults.DevContainerEnvFilename);
            if (!File.Exists(devContainerEnvFilename))
            {
                LogTo.Info($"Writing {devContainerEnvFilename}...");
                customVars.WriteEnvFile(devContainerEnvFilename);
            }
            else
                LogTo.Info($"Found {devContainerEnvFilename}, re-using");

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
            LogTo.Info($"Activating template {opts.Name} from {sourceTemplatePath}");

            var destPath = Path.Combine(sourceTemplatePath, "..");
            var devContainerEnvFilename = Path.Combine(destPath, Defaults.DevContainerEnvFilename);

            if (Extensions.RunningInContainer() &&
                ((Environment.GetEnvironmentVariable("HOST_USER_UID") == null) ||
                 (Environment.GetEnvironmentVariable("HOST_USER_GID") == null) ||
                 (Environment.GetEnvironmentVariable("HOST_USER_NAME") == null)))
            {
                Console.Error.WriteLine($"Can't init within a container without access to $HOST_USER_UID, $HOST_USER_GID or $HOST_USER_NAME");
                Environment.Exit(1);
            }
            else
                LogTo.Info("Running within devcontainer. Using $HOST_USER_UID, $HOST_USER_GID and $HOST_USER_NAME");

            var customVars = Extensions.RunningInContainer() ?
            new Dictionary<string, string> {
                { "HOST_USER_UID", Environment.GetEnvironmentVariable("HOST_USER_UID") },
                { "HOST_USER_GID", Environment.GetEnvironmentVariable("HOST_USER_GID") },
                { "HOST_USER_NAME", Environment.GetEnvironmentVariable("HOST_USER_NAME") }
            } :
            new Dictionary<string, string> {
                { "HOST_USER_UID", Cli.Wrap("id").SetArguments("-u").Execute().StandardOutput.Trim('\r','\n', ' ') },
                { "HOST_USER_GID", Cli.Wrap("id").SetArguments("-g").Execute().StandardOutput.Trim('\r','\n', ' ') },
                { "HOST_USER_NAME", Environment.GetEnvironmentVariable("USER") }
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
            
            LogTo.Info($"Appending user-specific env settings...");
            customVars.AppendToEnvFile(devContainerEnvFilename);
        }

        static void List(LSOptions opts)
        {
            var devcontainerFolder = Path.Combine(opts.Context, Defaults.DevContainerFolder);
            if (!Directory.Exists(devcontainerFolder))
            {
                LogTo.Info("No saved or current devcontainers!");
                Environment.Exit(0);
            }

            foreach (var subdir in Directory.GetDirectories(devcontainerFolder))
                Console.WriteLine(Path.GetFileName(subdir));

            var devcontainerJsonFilename = Path.Combine(devcontainerFolder, Defaults.DevContainerJsonFilename);
            if (File.Exists(devcontainerJsonFilename))
            {
                var devcontainerDesc = JsonConvert.DeserializeObject<DevcontainerDesc>(File.ReadAllText(devcontainerJsonFilename));
                Console.WriteLine($"[current] {devcontainerDesc.name}");
            }
            else
                Console.WriteLine($"[current] (none)");
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