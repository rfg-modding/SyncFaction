rmdir /s /q publish
dotnet clean
dotnet test
dotnet publish SyncFaction -o publish