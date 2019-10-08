FROM mcr.microsoft.com/dotnet/core/sdk

RUN apt-get update && \
    apt-get install -y git nano wget
