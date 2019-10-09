FROM mcr.microsoft.com/dotnet/core/sdk

ENV VERSION=${VERSION}

RUN apt-get update && \
    apt-get install -y git nano wget

WORKDIR /usr/local/src/devcontainer/
COPY . /usr/local/src/devcontainer/
RUN dotnet pack && \
    dotnet tool install --global --add-source devcontainer/nupkg devcontainer

ENV PATH="$PATH:/root/.dotnet/tools"