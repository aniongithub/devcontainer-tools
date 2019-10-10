# devcontainer-tools

## What is a devcontainer?
A devcontainer is a method to combine application sources and a Docker container with build tools/dependencies  to produce a binary without any local installation shenanigans.

All you need is:

* Docker
* docker-compose
* dotnet
* A Dockerfile that sets up the environment to build your application
* A build script

## Example

`dotnet tool install -g devcontainer`
`cd /to/project/dir`
`devcontainer new csharp`
`devcontainer new csharp`
`devcontainer run build.sh`

That's it! Now `devcontainer/nupkg/` will contain the output of the buildᙿ. Note that this can for any platform or architecture supported by your Docker engine.

*ᙿNote: devcontainer sets up the correct access rights of the logged on user, so any files produced are directly usable on the host*