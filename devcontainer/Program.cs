using CommandLine;
using System;
using System.Linq;

using devcontainer.core;

namespace devcontainer
{
    class Program
    {
        static void Main(string[] args)
        {
            // Parse command line arguments
            Parser.Default
                // Register all our verbs
                .ParseArguments<InitOptions, 
                                ActivateOptions,
                                DeactivateOptions,
                                LSOptions, 
                                RunOptions,
                                StartOptions,
                                StopOptions,
                                StatusOptions,
                                ExecOptions>(args)
                // Execute verb handlers
                .WithParsed<InitOptions>(opts => Init(opts))
                .WithParsed<ActivateOptions>(opts => Activate(opts))
                .WithParsed<DeactivateOptions>(opts => Deactivate(opts))
                .WithParsed<LSOptions>(opts => List(opts))
                .WithParsed<RunOptions>(opts => Run(opts))
                .WithParsed<StartOptions>(opts => Start(opts))
                .WithParsed<StopOptions>(opts => Stop(opts))
                .WithParsed<StatusOptions>(opts => Status(opts))
                .WithParsed<ExecOptions>(opts => Exec(opts))
                // Print any errors to stderr
                .WithNotParsed(err => Console.Error.WriteLine($"{string.Join(',', err)}"));
        }

        static void Init(InitOptions opts)
        {
            Devcontainer.Init(opts);
        }

        static void Activate(ActivateOptions opts)
        {
            Devcontainer.Activate(opts);
        }

        static void Deactivate(DeactivateOptions opts)
        {
            Devcontainer.Deactivate(opts);
        }

        static void List(LSOptions opts)
        {
            foreach (var desc in Devcontainer.List(opts))
                Console.WriteLine($"[{(desc.Value.active ? "curr" : "saved")}] {desc.Value.name}");
        }

        static void Run(RunOptions opts)
        {
            Devcontainer.Run(opts);
        }

        static void Start(StartOptions opts)
        {
            Devcontainer.Start(opts);
        }

        static void Stop(StopOptions opts)
        {
            Devcontainer.Stop(opts);
        }
        static void Status(StatusOptions opts)
        {
            var devcontainerName = (from desc in Devcontainer.List(new LSOptions { Context = opts.Context }) 
                                    where desc.Value.active 
                                    select desc.Value.name)
                                    .FirstOrDefault();
            if (devcontainerName == null)
            {
                Console.WriteLine($"No active devcontainers found in {opts.Context}");
                return;
            }
            var running = Devcontainer.Status(opts);
            Console.WriteLine($"Active devcontainer \"{devcontainerName}\" is {(running ? "running" : "not running")}");
        }
        static void Exec(ExecOptions opts)
        {
            Devcontainer.Exec(opts);
        }
    }
}