using CliWrap;

using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;

namespace devcontainer.core
{
    public static class Devcontainer
    {
        private static bool RunningInContainer()
        {
            var filename = $"/proc/1/cgroup";
            if (!File.Exists(filename))
                return false;
            var text = File.ReadAllText(filename);
            return text.Contains("/docker/");
        }
        
        public static bool Init(IInitOptions opts)
        {
            try
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
                var name = opts.Name ?? opts.TemplateName;
                
                // Find the source template
                var sourceTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Defaults.TemplatesPath, opts.TemplateName);
                if (!Directory.Exists(sourceTemplatePath))
                {
                    Console.Error.WriteLine($"Could not find template {opts.TemplateName}!");
                    return false;
                }
                else
                    Console.WriteLine($"Found {opts.TemplateName}");

                // Create blank Dockerfile if needed
                if (!File.Exists(Path.Combine(opts.Context, opts.Dockerfile)))
                {
                    Console.Error.WriteLine($"No Dockerfile was found in \"{opts.Context}\", creating blank Dockerfile {opts.Dockerfile}. Please update this to install any prerequisites your build environment needs");
                    opts.Context.EnsureDirectoriesExist();
                    File.WriteAllText(Path.Combine(opts.Context, opts.Dockerfile), Defaults.DefaultDockerfileContents);
                }
                else
                    Console.WriteLine($"{opts.Dockerfile} found, will use to build base image");

                // Create .devcontainer folder
                var devContainerFolder = Path.Combine(opts.Context, Defaults.DevContainerFolder)
                    .EnsureDirectoriesExist();

                // Create our destination template folder
                var destTemplatePath = Path.Combine(devContainerFolder, name)
                    .EnsureDirectoriesExist();

                // Create devcontainer.env file
                var devContainerEnvFilename = Path.Combine(destTemplatePath, Defaults.DevContainerEnvFile);
                if (!File.Exists(devContainerEnvFilename))
                {
                    Console.WriteLine($"Writing {devContainerEnvFilename}...");
                    customVars.WriteEnvFile(devContainerEnvFilename);
                }
                else
                    Console.WriteLine($"Found {devContainerEnvFilename}, re-using");

                // Copy to our destination path
                // Process variables, but pass through unknowns
                sourceTemplatePath.CopyTo(destTemplatePath, overwrite: opts.Overwrite,
                    onCopyFile: (src, dst) => customVars.Process(src, dst, opts.Overwrite, passthroughUnknowns: true));

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not initialize devcontainer! {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        // TODO: Add timeout and cancellation support for hook execution
        public static bool Activate(IActivateOptions opts)
        {
            try
            {
                var sourceTemplatePath = Path.Combine(Environment.CurrentDirectory, Defaults.DevContainerFolder, opts.Name);
                if (!Directory.Exists(sourceTemplatePath))
                {
                    Console.Error.WriteLine($"Cannot find saved devcontainer with name {opts.Name}");
                    return false;
                }
                Console.WriteLine($"Activating template {opts.Name} from {sourceTemplatePath}");

                if (!opts.DisableHooks)
                {
                    // Is there a pre-activate hook?
                    var templatePath = Path.Combine(Environment.CurrentDirectory, Defaults.DevContainerFolder, opts.Name);
                    var preActivateHook = Path.Combine(templatePath, Defaults.PreActivateHook);
                    if (File.Exists(preActivateHook))
                    {
                        // Yes, run it
                        Console.WriteLine($"Found pre-activate hook for template {opts.Name} from {sourceTemplatePath}");
                        try
                        {
                            var templateEnv = Path.Combine(Environment.CurrentDirectory, Defaults.DevContainerFolder, opts.Name, Defaults.DevContainerEnvFile)
                                .LoadEnvFile();
                            var result = Cli.Wrap(preActivateHook)
                                .SetEnvironmentVariables(templateEnv)
                                .SetWorkingDirectory(Environment.CurrentDirectory)
                                .SetStandardOutputCallback(l => Console.WriteLine(l))
                                .SetStandardErrorCallback(l => Console.Error.WriteLine(l))
                                .Execute();
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Error running pre-activate hook! {ex.Message}\n{ex.StackTrace}");
                            return false;
                        }
                    }
                }
                else
                    Console.WriteLine($"Hooks disabled for {opts.Name} from {sourceTemplatePath}");

                var destPath = Path.Combine(sourceTemplatePath, "..");
                var devContainerEnvFilename = Path.Combine(destPath, Defaults.DevContainerEnvFile);

                if (RunningInContainer() &&
                    ((Environment.GetEnvironmentVariable("HOST_USER_UID") == null) ||
                    (Environment.GetEnvironmentVariable("HOST_USER_GID") == null) ||
                    (Environment.GetEnvironmentVariable("HOST_USER_NAME") == null)))
                {
                    Console.Error.WriteLine($"Can't initialize within a container without access to $HOST_USER_UID, $HOST_USER_GID or $HOST_USER_NAME");
                    return false;
                }
                else
                    Console.WriteLine("Found, using $HOST_USER_UID, $HOST_USER_GID and $HOST_USER_NAME");

                var customVars = RunningInContainer() ?
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
                
                Console.WriteLine($"Appending user-specific env settings...");
                customVars.AppendToEnvFile(devContainerEnvFilename);

                if (!opts.DisableHooks)
                {
                    // Is there a post-activate hook?
                    var templatePath = Path.Combine(Environment.CurrentDirectory, Defaults.DevContainerFolder, opts.Name);
                    var postActivateHook = Path.Combine(templatePath, Defaults.PostActivateHook);
                    if (File.Exists(postActivateHook))
                    {
                        // Yes, run it
                        Console.WriteLine($"Found post-activate hook for template {opts.Name} from {sourceTemplatePath}");
                        try
                        {
                            var templateEnv = Path.Combine(Environment.CurrentDirectory, Defaults.DevContainerFolder, opts.Name, Defaults.DevContainerEnvFile)
                                .LoadEnvFile();
                            var result = Cli.Wrap(postActivateHook)
                                .SetEnvironmentVariables(templateEnv)
                                .SetWorkingDirectory(Environment.CurrentDirectory)
                                .SetStandardOutputCallback(l => Console.WriteLine(l))
                                .SetStandardErrorCallback(l => Console.Error.WriteLine(l))
                                .Execute();
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Error running post-activate hook! {ex.Message}\n{ex.StackTrace}");
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not initialize devcontainer! {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public static IEnumerable<KeyValuePair<string, DevcontainerDesc>> List(ILSOptions opts)
        {
            var devcontainerFolder = Path.Combine(opts.Context, Defaults.DevContainerFolder);
            if (!Directory.Exists(devcontainerFolder))
            {
                Console.Error.WriteLine("No saved or current devcontainers!");
                yield break;
            }

            // Enumerate and load devcontainer.json for all saved devcontainers
            foreach (var subdir in Directory.GetDirectories(devcontainerFolder))
            {
                var descPath = Path.Combine(subdir, Defaults.DevcontainerJsonFile);
                if (File.Exists(descPath))
                    yield return new KeyValuePair<string, DevcontainerDesc>(devcontainerFolder, JsonConvert.DeserializeObject<DevcontainerDesc>(File.ReadAllText(descPath)));
            }

            var activeDescPath = Path.Combine(devcontainerFolder, Defaults.DevcontainerJsonFile);
            if (File.Exists(activeDescPath))
            {
                var desc = JsonConvert.DeserializeObject<DevcontainerDesc>(File.ReadAllText(activeDescPath));
                // Mark this one as active because it was in .devcontainer/
                desc.active = true; 
                
                yield return new KeyValuePair<string, DevcontainerDesc>(devcontainerFolder, desc);
            }
            else
            {
                // Didn't find any active devcontainers
                yield return new KeyValuePair<string, DevcontainerDesc>(devcontainerFolder, 
                new DevcontainerDesc
                {
                    active = true,
                    name = "(none)"
                });
            }                
        }

        public static bool Run(IRunOptions opts)
        {
            var devcontainerFolder = Path.Combine(opts.Context, Defaults.DevContainerFolder);
            if (!Directory.Exists(devcontainerFolder))
            {
                Console.Error.WriteLine("No saved or current devcontainers!");
                return false;
            }
            var env = Path.Combine(devcontainerFolder, Defaults.DevContainerEnvFile).LoadEnvFile();
            var activeDescPath = Path.Combine(devcontainerFolder, Defaults.DevcontainerJsonFile);
            if (!File.Exists(activeDescPath))
            {
                Console.Error.WriteLine("No current devcontainers!");
                return false;
            }

            // Try to load up our current devcontainer.json
            var desc = JsonConvert.DeserializeObject<DevcontainerDesc>(File.ReadAllText(activeDescPath));
            try
            {
                // Use 'docker-compose run' to kick off command execution
                var workDir = Path.Combine(desc.workspaceFolder, opts.WorkDir);
                var result = Cli.Wrap("docker-compose")
                    .SetWorkingDirectory(devcontainerFolder)
                    .SetArguments($"run -w {workDir} --rm {desc.service} {opts.Command}")
                    .SetStandardOutputCallback(l => Console.WriteLine(l))
                    .SetStandardErrorCallback(l => Console.Error.WriteLine(l))
                    .Execute();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not run {opts.Command}! Exception: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public static bool Start(IStartOptions opts)
        {
            var devcontainerFolder = Path.Combine(opts.Context, Defaults.DevContainerFolder);
            if (!Directory.Exists(devcontainerFolder))
            {
                Console.Error.WriteLine("No saved or current devcontainers!");
                return false;
            }
            var env = Path.Combine(devcontainerFolder, Defaults.DevContainerEnvFile).LoadEnvFile();
            var activeDescPath = Path.Combine(devcontainerFolder, Defaults.DevcontainerJsonFile);
            if (!File.Exists(activeDescPath))
            {
                Console.Error.WriteLine("No current devcontainers!");
                return false;
            }
            var desc = JsonConvert.DeserializeObject<DevcontainerDesc>(File.ReadAllText(activeDescPath));
            try
            {
                // Build all services, but don't start yet
                var result = Cli.Wrap("docker-compose")
                    .SetWorkingDirectory(devcontainerFolder)
                    .SetArguments($"up {(opts.Build ? "--build" : "")} --no-start {desc.service}")
                    .SetStandardOutputCallback(l => Console.WriteLine(l))
                    .SetStandardErrorCallback(l => Console.Error.WriteLine(l))
                    .Execute();

                // Start the service we care about and any dependencies
                // TODO: Add more options that can be passed in to docker-compose start
                result = Cli.Wrap("docker-compose")
                    .SetWorkingDirectory(devcontainerFolder)
                    .SetArguments($"start {desc.service}")
                    .SetStandardOutputCallback(l => Console.WriteLine(l))
                    .SetStandardErrorCallback(l => Console.Error.WriteLine(l))
                    .Execute();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not start devcontainer {desc.name}! Exception: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public static bool Stop(IStopOptions opts)
        {
            var devcontainerFolder = Path.Combine(opts.Context, Defaults.DevContainerFolder);
            if (!Directory.Exists(devcontainerFolder))
            {
                Console.Error.WriteLine("No saved or current devcontainers!");
                return false;
            }
            var env = Path.Combine(devcontainerFolder, Defaults.DevContainerEnvFile).LoadEnvFile();
            var activeDescPath = Path.Combine(devcontainerFolder, Defaults.DevcontainerJsonFile);
            if (!File.Exists(activeDescPath))
            {
                Console.Error.WriteLine("No current devcontainers!");
                return false;
            }
            var desc = JsonConvert.DeserializeObject<DevcontainerDesc>(File.ReadAllText(activeDescPath));
            try
            {
                // docker-compose stop
                var result = Cli.Wrap("docker-compose")
                    .SetWorkingDirectory(devcontainerFolder)
                    .SetArguments($"stop --timeout {opts.TimeoutSec} {desc.service}")
                    .SetStandardOutputCallback(l => Console.WriteLine(l))
                    .SetStandardErrorCallback(l => Console.Error.WriteLine(l))
                    .Execute();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not stop devcontainer {desc.name}! Exception: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public static bool Status(IStatusOptions opts)
        {
            var devcontainerFolder = Path.Combine(opts.Context, Defaults.DevContainerFolder);
            if (!Directory.Exists(devcontainerFolder))
            {
                Console.Error.WriteLine("No saved or current devcontainers!");
                return false;
            }
            var env = Path.Combine(devcontainerFolder, Defaults.DevContainerEnvFile).LoadEnvFile();
            var activeDescPath = Path.Combine(devcontainerFolder, Defaults.DevcontainerJsonFile);
            if (!File.Exists(activeDescPath))
            {
                Console.Error.WriteLine("No current devcontainers!");
                return false;
            }
            var desc = JsonConvert.DeserializeObject<DevcontainerDesc>(File.ReadAllText(activeDescPath));
            var psList = new List<string>();

            // Check if service is running with docker-compose 
            // https://serverfault.com/a/935674/543369
            try
            {
                bool running = false;
                var result = Cli.Wrap("docker")
                    .SetWorkingDirectory(devcontainerFolder)
                    .SetArguments($"ps -q --no-trunc")
                    .SetStandardOutputCallback(l => psList.Add(l))
                    .SetStandardErrorCallback(l => Console.Error.WriteLine(l))
                    .Execute();

                result = Cli.Wrap("docker-compose")
                    .SetWorkingDirectory(devcontainerFolder)
                    .SetArguments($"ps -q {desc.service}")
                    .SetStandardOutputCallback(l => running = psList.Contains(l))
                    .SetStandardErrorCallback(l => Console.Error.WriteLine(l))
                    .Execute();

                return running;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could check status of devcontainer {desc.name}! Exception: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public static bool Exec(IExecOptions opts)
        {
            var devcontainerFolder = Path.Combine(opts.Context, Defaults.DevContainerFolder);
            if (!Directory.Exists(devcontainerFolder))
            {
                Console.Error.WriteLine("No saved or current devcontainers!");
                return false;
            }
            var env = Path.Combine(devcontainerFolder, Defaults.DevContainerEnvFile).LoadEnvFile();
            var activeDescPath = Path.Combine(devcontainerFolder, Defaults.DevcontainerJsonFile);
            if (!File.Exists(activeDescPath))
            {
                Console.Error.WriteLine("No current devcontainers!");
                return false;
            }
            var desc = JsonConvert.DeserializeObject<DevcontainerDesc>(File.ReadAllText(activeDescPath));
            try
            {
                // Because we're not modifying our docker-compose.yml,
                // we can find the container that's running our service
                var workDir = Path.Combine(desc.workspaceFolder, opts.WorkDir);
                string container = null;
                var result = Cli.Wrap("docker-compose")
                    .SetWorkingDirectory(devcontainerFolder)
                    .SetArguments($"ps -q {desc.service}")
                    .SetStandardOutputCallback(l => container = l)
                    .SetStandardErrorCallback(l => Console.Error.WriteLine(l))
                    .Execute();
                
                if (container == null)
                {
                    Console.Error.WriteLine($"Could not find service {desc.service} from devconainer {desc.name}!");
                    return false;
                }
                
                // and simply docker exec our command in it
                Cli.Wrap("docker")
                    .SetWorkingDirectory(devcontainerFolder)
                    .SetArguments($"exec -d -w {workDir} {container} {opts.Command}")
                    .Execute();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not execute {opts.Command}! Exception: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }
}
