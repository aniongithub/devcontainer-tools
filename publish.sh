#! /bin/bash
ARCH=$(eval uname -p)
case ${ARCH} in
    "armv7l")
        RID="linux-arm"
        ;;
    "x86_64")
        RID="linux-x64"
        ;;
    *)
        exit 1
        ;;
esac
dotnet publish -c Release -r ${RID} --self-contained true /p:PublishTrimmed=true /p:PublishSingleFile=true