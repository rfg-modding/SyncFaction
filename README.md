# SyncFaction

Red Faction Guerrilla mod manager with focus on user experience

*Like a multiplayer launcher for any recent game, plus mod support*

![screenshot](screenshot.png)

## Project goals

* Provide decent **multiplayer** experience: 1-click sync to latest game version with tonight's map pack and jump into action!
* Become ModManager successor for **singleplayer** with less bugs and more online package-management approach
* Save people from headaches (manual file management, keeping up to date with community)

## Features

* Both Steam and GOG versions are supported
* Community patch auto-update
  * Incremental updates supported
  * Patch is not released officially as of September 2022, sort of alpha version is used by community
* Backup management tries to save you from downloading game files over and over again if something got messed up
* Integration with [Faction Files](https://www.factionfiles.com/ff.php?action=files)
  * Browse, download and install mods _(only multiplayer maps for now)_
  * See news for upcoming Game Nights: _will tell you which map pack we are playing next!_
* Download progress display, cache and resume for places with bad internet

## Non-features

* Saving storage space is not a priority. Backups are created only when files are modified for the first time, but after extensive modding game folder can be 2x heavier (or more!) because every game file is stored twice for quick restore
* Unpacking game archives and internal file formats for modding is done by Nanoforge and other tools

## Getting help

If app does not start and leads you to Microsoft downloads page, you need the [x64 .NET 6.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime) **FOR DESKTOP APPS**

![dotnet desktop runtime](https://user-images.githubusercontent.com/1562341/204090216-2d163e9c-b60e-4e45-88e2-bcd4f21aab69.png)

Game and app related issues:

* Use Steam or GOG Galaxy to check integrity of game files
* Check if game location is valid
* See if new versions of SyncFaction are available on Github
* Please report errors here in issues. **Create and copy diagnostics report** to help fixing it! Also describe what you were trying to do.
* Probably ping **rast1234** on FactionFiles Discord (link below)

## Tech info

Built using `dotnet 6`. UI is using WPF, core functionality is separated to a class library. Currently tied to Windows because of WPF and Registry (used to find game installation). Linux port only requires another UI or CLI.

Release is always single-file .exe with only dependency on dotnet framework installed (will ask to download if not).

All settings, cache, backups and downloads are stored inside `<game_dir>/.syncfaction`. Folder can be removed at any time.

## Credits

Implemented with support from **moneyl, Camo, Goober, natalie, ATMLVE** and others from FactionFiles community. You people are awesome, bringing life to my favourite game! <3

([join Discord!](https://discord.gg/factionfiles))

## TODO

0. ~~XML merging features~~
  * ~~stream read-write vpp to avoid high memory usage~~
  * ~~write to tmp, then delete src and rename tmp to src~~
  * ~~implement LIST_ACTIONs from MM decompiled logic~~
  * ~~restore vpp files from backup~~
  * ~~skip recompressing untouched files~~
  * ~~test all existing mods~~
  * ~~file add/replace logic should be the same for folder and modinfo mods~~
  * ~~forget/reset inputs in UI?~~
  * ~~proper error handling and cleanup~~
1. ~~introduce VPP repacking support for mods/updates~~
2. ~~rewrite updater~~
3. ~~track new separate set of search strings from FF - for RSL2~~
4. ~~compare gog and steam distributions~~
  * ~~some archives can't be xdelta'd. see if they have entries in same order so we cau update with loose files~~
  * ~~apply xdelta on steam files to match gog: `terr01_precache vehicles effects effects_mp chunks`~~
  * ~~`terr01_l1.vpp_pc` must be repackaged in both game versions with files from gog~~
7. ~~backport stuff from other branches: user agent? something else?~~
8. ~~hash checks~~
  * ~~button to generate json of all files~~
    * ~~record last modified time (probably others too)~~
    * ~~byte size~~
    * ~~hash~~
    * ~~order dictionary by file names and attributes so it's always easy to compare~~
    * ~~make a list of all files in game dir?~~
    * ~~instruct users to copy-paste to help troubleshoot~~
  * ~~use hashlist file from mod/patch to verify after install~~
  * ~~rollback if hashlist check failed? need to do something here~~
  * ~~remember if last hashlist check completed or not, this will tell if user abruptly closed SF while installing~~
  * ~~test hash check~~
12. ~~Fix patch not installing lol~~
16. ~~placeholder text when modinfo has no options?~~
17. ~~better messages~~
    * ~~fix logging~~
    * ~~fix formatting~~
    * ~~user-friendly messages~~
    * ~~proper operations log~~
    * ~~fix progress display~~
    * ~~sane error formatting/handling~~
    * ~~restore buttons should update mod list / ui~~
    * ~~modinfo description should be wrapped and have some offset~~
20. ~~support mods working differently for steam/gog, eg with subfolders~~
21. ~~"restore to modded" - load list of mods back (should be stored separately)~~
22. ~~state is saved even if initial check isnt finished - is this OK?~~
24. ~~remove usings of FileInfo, DirectoryInfo, Path (use abstractions lib)~~
25. ~~log every file/folder manipulation: create, write, delete, move, copy~~
26. ~~log.trace every IF~~
27. ~~fix TODOs~~
28. ~~make xdelta work for managed files~~
29. ~~test what happens if cdn host is unavailable~~
30. ~~rickroll~~
33. ~~run with Launcher.exe if found (RSL2)~~
34. ~~dev mode should not ignore update check. make separate checkbox for it and save~~


## Release

* fix TODOs left from debugging
* rename FF categories as MM/legacy/something, add new for SF mods
* remove stuff from news page: links dont work, formatting is bad, a lot of content is bad
* remove hack for old update IDs (rfgcommunityupdate)
* see if logs look nice without dev mode and with it
* test on both steam and gog versions
* test with terraform rebuild
* simulate terraform rebuild and RSL uploaded to FF
* rename/move/create FF categories
* document modinfo.xml
* document mod structure and all the magic
* make examples
  * to avoid clutter, instruct users to place modinfo.xml and all relative stuff into a subfolder
* testing
  * loose vpp files
  * new vpps
  * modinfo in subdir
  * combinations of stuff
  * hash file
  * test if preserve whitespace works for files like anim_files.xtbl - they should not be formatted/minimized


## Backlog

* fix jumping text on devmode toggle
* port/network checker?
  * inspect how game uses network for client and host modes
  * check if traffic can flow as required by either mode
  * need a server side app for this
