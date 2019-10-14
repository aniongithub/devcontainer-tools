FROM mcr.microsoft.com/dotnet/core/sdk

ENV VERSION=${VERSION}

RUN apt-get update && \
    apt-get install -y git nano wget