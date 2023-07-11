# /bin/bash -ex
rm -rf _publish
dotnet clean src
dotnet test src || exit 1
dotnet publish src/SyncFaction -o _publish
dotnet publish src/SyncFaction.Toolbox -o _publish
