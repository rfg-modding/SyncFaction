# Tech info

Built using `dotnet 6`. UI is using WPF, core functionality is separated to a class library. Currently tied to Windows because of WPF and Registry (used to find game installation). Linux port would only require migration from WPF to Avalonia.

Release is always a single-file .exe with only dependency on dotnet framework installed (will ask to download if not).

All settings, cache, backups and downloads are stored inside `<game_dir>/.syncfaction`. Folder can be removed at any time.

## Project structure

* `SyncFaction` - app with UI
* `SyncFaction.Core` - main logic and features, separated from UI
* `SyncFaction.Extras` - helpers for auto-generated build version
* `SyncFaction.Extras.CodeGenerator` - helpers for auto-generated build version
* `SyncFaction.ModManager` - modinfo.xml support, recursive merging algorithms for xml files
* `SyncFaction.Packer` - VPP archive reader/writer, optimized for streaming
* `SyncFaction.Toolbox` - CLI with tools useful for development, eg. selective archive extractor and metadata/checksum calculator
* `tests/*` - tests for non-trivial code like vpp archiver and backup file management
