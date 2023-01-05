# /bin/bash -ex
rm -rf publish
dotnet clean
dotnet test || exit 1
dotnet publish SyncFaction -o publish
