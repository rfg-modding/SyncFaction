# /bin/bash -ex
rm -rf publish
dotnet clean src
dotnet test src || exit 1
dotnet publish src/SyncFaction -o publish
dotnet publish src/SyncFaction.Toolbox -o publish
