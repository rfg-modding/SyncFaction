# /bin/bash -ex
rm -rf _publish
dotnet clean src
dotnet publish src/SyncFaction.Toolbox -o _publish
