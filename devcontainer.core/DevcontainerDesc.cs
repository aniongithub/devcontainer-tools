using System.Collections.Generic;

namespace devcontainer
{
    public sealed class DevcontainerDesc
    {
        public string name { get; set; }
        public string workspaceFolder { get; set; }
        public string dockerComposeFile { get; set; }
        public string service { get; set; }
        public string shutdownAction { get; set; }

        public bool active { get; internal set; }
    }
}