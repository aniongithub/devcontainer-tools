using System;

namespace devcontainer.core
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

        public const string DefaultEnvFile = ".env";
        // public const string DevContainerEnvFile = "devcontainer.env";
        public const string PreActivateHookEnvFile = "pre-activate.env";
        public const string PostActivateHookEnvFile = "post-activate.env";
        public const string PreInitializeHookEnvFile = "pre-initialize.env";
        public const string PostInitializeHookEnvFile = "post-initialize.env";

        public const string PreDeactivateHookEnv = "pre-deactivate.env";
        public const string PostDeactivateHookEnv = "post-deactivate.env";

        public const string DefaultDockerfileContents = @"FROM scratch
# TODO: Install any dependencies or tools here ...";

        public const string PreActivateHook = "pre-activate";
        public const string PostActivateHook = "post-activate";
        
        public const string PreDeactivateHook = "pre-deactivate";
        public const string PostDeactivateHook = "post-deactivate";

        public const string PreInitializeHook = "pre-initialize";
        public const string PostInitializeHook = "post-initialize";
    }

}
