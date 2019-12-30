using System.Xml.Linq;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using CliWrap;

using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;
using System.Text.RegularExpressions;

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
                    { "DEVCONTAINER_WORKSPACE_ROOT", opts.WorkspaceRoot },
                    { "DEVCONTAINER_ENV_FILE", Defaults.DefaultEnvFile }
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

                var templateEnv = Path.Combine(sourceTemplatePath, Defaults.DefaultEnvFile).LoadEnvFile();
                if (!opts.DisableHooks)
                {
                    // Is there a pre-initialize hook?
                    var preInitializeHook = Path.Combine(sourceTemplatePath, Defaults.PreInitializeHook);

                    // Precedence is templateEnv < preInitializeHookEnv
                    var preInitializeHookEnv = Path.Combine(sourceTemplatePath, Defaults.PreInitializeHookEnvFile).LoadEnvFile();
                    var mergedPreInitializeEnv = templateEnv
                        .MergeWithUpdates(preInitializeHookEnv);

                    // Execute the hook
                    Console.WriteLine($"Executing pre-initialize hook for template {opts.Name} from {sourceTemplatePath}");
                    if (!preInitializeHook.ExecuteHook(mergedPreInitializeEnv, environment: templateEnv))
                        return false;
                }
                else
                    Console.WriteLine($"Hooks disabled for {opts.Name} from {sourceTemplatePath}");

                // Create blank Dockerfile if needed
                if (!File.Exists(Path.Combine(opts.Context, opts.Dockerfile)))
                {
                    Console.Error.WriteLine($"No Dockerfile was found in \"{opts.Context}\", creating blank Dockerfile {opts.Dockerfile}\nPlease update this to install any prerequisites your environment needs");
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

                // Copy to our destination path
                sourceTemplatePath.CopyTo(destTemplatePath, overwrite: opts.Overwrite,
                    onCopyFile: (source, dest) => File.WriteAllText(dest,
                        Extensions.PerformTemplateSubstitutions(File.ReadAllText(source), customVars, 
                        passthroughUnknowns: true)));
                
                // Create/merge .env file
                var devContainerEnvFilename = Path.Combine(destTemplatePath, Defaults.DefaultEnvFile);
                devContainerEnvFilename.CreateOrMerge(customVars);

                if (!opts.DisableHooks)
                {
                    // Is there a post-initialize hook?
                    var postInitializeHook = Path.Combine(destTemplatePath, Defaults.PostInitializeHook);

                    // Precedence is templateEnv < postInitializeHookEnv
                    var postInitializeHookEnv = Path.Combine(destTemplatePath, Defaults.PostInitializeHookEnvFile).LoadEnvFile();
                    var mergedPostInitializeEnv = templateEnv
                        .MergeWithUpdates(postInitializeHookEnv);

                    // Execute the hook
                    Console.WriteLine($"Executing post-initialize hook for template {opts.Name} from {destTemplatePath}");
                    if (!postInitializeHook.ExecuteHook(mergedPostInitializeEnv, environment: templateEnv))
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not initialize devcontainer! {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        // Regex to find variables for entry/replacement
        private static readonly Regex RequiredVariables = new Regex(@"\$\{(?<variable>\w+)(\?(?<prompt>[^-:=}]+)})", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

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

                var templateEnv = Path.Combine(sourceTemplatePath, Defaults.DefaultEnvFile).LoadEnvFile();

                if (!opts.DisableHooks)
                {
                    // Is there a pre-activate hook?
                    var preActivateHook = Path.Combine(sourceTemplatePath, Defaults.PreActivateHook);

                    // Precedence is templateEnv < preActivateHookEnv
                    var preActivateHookEnv = Path.Combine(sourceTemplatePath, Defaults.PreActivateHookEnvFile).LoadEnvFile();
                    var mergedPreActivateEnv = templateEnv
                        .MergeWithUpdates(preActivateHookEnv);

                    // Execute the hook
                    Console.WriteLine($"Executing pre-activate hook for template {opts.Name} from {sourceTemplatePath}");
                    if (!preActivateHook.ExecuteHook(mergedPreActivateEnv, environment: templateEnv))
                        return false;
                }
                else
                    Console.WriteLine($"Hooks disabled for {opts.Name} from {sourceTemplatePath}");

                var destPath = Path.Combine(sourceTemplatePath, "..");

                // Are we running this command inside a container? If so, we need these vars
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

                // Precedence is templateEnv < customVars
                var mergedEnv = templateEnv
                    .MergeWithUpdates(customVars);

                // Activate as current devcontainer and perform substitutions
                sourceTemplatePath.CopyTo(destPath, overwrite: opts.Overwrite,
                    onCopyFile: (source, dest) => File.WriteAllText(dest,
                        Extensions.PerformTemplateSubstitutions(File.ReadAllText(source), mergedEnv, 
                        passthroughUnknowns: true)));

                // Write .env in destination path
                var defaultEnvFilename = Path.Combine(destPath, Defaults.DefaultEnvFile);
                Console.WriteLine($"Writing environment...");
                mergedEnv.WriteEnvFile(defaultEnvFilename);

                if (!opts.DisableHooks)
                {
                    var postActivateHook = Path.Combine(destPath, Defaults.PostActivateHook);

                    // Precedence is templateEnv < preActivateHookEnv
                    var postActivateHookEnv = Path.Combine(destPath, Defaults.PostActivateHookEnvFile).LoadEnvFile();
                    var mergedPostActivateEnv = templateEnv
                        .MergeWithUpdates(postActivateHookEnv);

                    // Execute the hook
                    Console.WriteLine($"Executing post-activate hook for template {opts.Name} from {destPath}");
                    if (!postActivateHook.ExecuteHook(mergedPostActivateEnv, environment: templateEnv))
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not initialize devcontainer! {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public static bool Deactivate(IDeactivateOptions opts)
        {
            try
            {
                var activeTemplatePath = Path.Combine(Defaults.Context, Defaults.DevContainerFolder);
                var activeDescPath = Path.Combine(activeTemplatePath, Defaults.DevcontainerJsonFile);
                if (!File.Exists(activeDescPath))
                {
                    Console.Error.WriteLine("No active devcontainers, nothing to do!");
                    return true;
                }
                var desc = JsonConvert.DeserializeObject<DevcontainerDesc>(File.ReadAllText(activeDescPath));
                Console.WriteLine($"Deactivating {desc.name}");

                var templateEnv = Path.Combine(activeTemplatePath, Defaults.DefaultEnvFile).LoadEnvFile();

                if (!opts.DisableHooks)
                {
                    var preDeactivateHook = Path.Combine(activeTemplatePath, Defaults.PreDeactivateHook);
                    var preDeactivateHookEnv = Path.Combine(activeTemplatePath, Defaults.PreDeactivateHookEnv).LoadEnvFile();

                    // Precedence is templateEnv < preDeactivateHookEnv
                    var mergedPredeactivateEnv = templateEnv.MergeWithUpdates(preDeactivateHookEnv);
                    Console.WriteLine($"Executing pre-deactivate hook for template {desc.name} from {activeTemplatePath}");
                    if (!preDeactivateHook.ExecuteHook(mergedPredeactivateEnv, environment: templateEnv))
                        return false;

                    // Delete all files in the active devcontainer folder
                    foreach (var file in Directory.EnumerateFiles(activeTemplatePath))
                        if ((Path.GetFileName(file) != Defaults.PostDeactivateHook) || (Path.GetFileName(file) != Defaults.PostDeactivateHookEnv))
                        {
                            Console.WriteLine($"Removing {file}");
                            File.Delete(file);
                        }

                    var postDeactivateHook = Path.Combine(activeTemplatePath, Defaults.PostDeactivateHook);
                    var postDeactivateHookEnvFilename = Path.Combine(activeTemplatePath, Defaults.PostDeactivateHookEnv);
                    var postDeactivateHookEnv = postDeactivateHookEnvFilename.LoadEnvFile();

                    // Precedence is templateEnv < preDeactivateHookEnv
                    var mergedPostDeactivateEnv = templateEnv.MergeWithUpdates(postDeactivateHookEnv);
                    Console.WriteLine($"Executing post-deactivate hook for template {desc.name} from {activeTemplatePath}");
                    if (!postDeactivateHook.ExecuteHook(mergedPostDeactivateEnv, environment: templateEnv))
                        return false;

                    if (File.Exists(postDeactivateHookEnvFilename))
                    {
                        Console.WriteLine($"Removing {postDeactivateHookEnvFilename}");
                        File.Delete(postDeactivateHookEnvFilename);
                    }
                    if (File.Exists(postDeactivateHook))
                    {
                        Console.WriteLine($"Removing {postDeactivateHook}");
                        File.Delete(postDeactivateHook);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not initialize devcontainer! {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            return true;
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
            var env = Path.Combine(devcontainerFolder, Defaults.DefaultEnvFile).LoadEnvFile();
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
            var env = Path.Combine(devcontainerFolder, Defaults.DefaultEnvFile).LoadEnvFile();
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
            var env = Path.Combine(devcontainerFolder, Defaults.DefaultEnvFile).LoadEnvFile();
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
            var env = Path.Combine(devcontainerFolder, Defaults.DefaultEnvFile).LoadEnvFile();
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
            var env = Path.Combine(devcontainerFolder, Defaults.DefaultEnvFile).LoadEnvFile();
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
