# /bin/bash -ex
rm -rf _publish
dotnet clean src
dotnet test src || exit 1
dotnet publish src/SyncFaction -o _publish -c Debug -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishReadyToRun=true
