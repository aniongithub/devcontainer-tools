using System.Collections.Generic;
using Newtonsoft.Json;

namespace devcontainer
{
    public sealed class DevcontainerDesc
    {
        public string name { get; set; }
        public string context { get; set; }
        public string workspaceFolder { get; set; }
        public string dockerComposeFile { get; set; }
        public string service { get; set; }
        public string shutdownAction { get; set; }

        public bool active { get; internal set; }

        private readonly Dictionary<string, string> _remoteEnv = new Dictionary<string, string>();
        public IDictionary<string, string> remoteEnv => _remoteEnv;

        private readonly Settings _settings = new Settings();
        public Settings settings => _settings;

        public sealed class Settings
        {
            [JsonProperty("terminal.integrated.shell.linux")]
            public string TerminalIntegratedShellLinux { get; set; }
        }
    }
}