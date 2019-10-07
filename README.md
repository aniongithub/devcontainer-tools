# devcontainer-tools

This repository contains a .NET core global tool that runs creates a devcontainer (a la VS code) and then allows you to execute scripts within it. The tool will also transparently handle user id management on all operating systems (as much as possible) so files created while within the container aren't created as root, but as the currently logged in user.