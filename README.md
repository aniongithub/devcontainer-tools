# devcontainer-tools

`devcontainer` is a way to combine application sources and a Docker container which packages build tools/dependencies to produce a binary in a snap - all without local installation shenanigans or ever mucking around with permissions/volumes. Also, because `devcontainer` uses the same file formats and structures used by the [Visual Studio Remote Containers](https://code.visualstudio.com/docs/remote/containers) extensions, this tool also acts as a handy way to manage and use multiple devcontainer configurations. If Visual Studio Code is installed, it can instantly act as an IDE with full intellisense and debugging support right out of the box.

All you need is:

### Installed on the Host
* [Docker](https://docs.docker.com/v17.09/engine/installation/)
* [docker-compose](https://docs.docker.com/compose/install/)
* [.NET Core 3.0+](https://dotnet.microsoft.com/download)
* [Visual Studio code](https://code.visualstudio.com/) with Remote Containers extension *(optional)*

### Per project
* A Dockerfile that sets up the environment to build your application (this file will not be modified, but used)
* Any source files or assets you may need

## Example

`dotnet tool install -g devcontainer`
`cd /to/project/dir`
`devcontainer new csharp`
`devcontainer activate csharp`
`~~devcontainer run build.sh~~`

alternatively, open Visual Studio Code and choose "Reopen in Container" from the Command menu.

That's it! Now `devcontainer/nupkg/` will contain the output of the buildᙿ. Note that this can for any platform or architecture supported by your Docker engine. 

*ᙿNote: devcontainer sets up the correct access rights of the logged on user, so any files produced are directly usable on the host*