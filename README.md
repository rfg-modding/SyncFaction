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

* Saving storage space is not a priority. Backups are created only when files are modified for the first time, but after extensive modding game folder can be 2x heavier (or more!) because every game file is stored twice
* Unpacking game archives and internal file formats for modding is done by Nanoforge and other tools

## Getting help

* Use Steam to check integrity of game files
* Check if game location is valid
* See if new versions of SyncFaction are available on Github
* Please report errors here in issues. **Copy all the stuff from application window** to help fixing it! Also describe what you were trying to do.
* Probably ping **rast1234** on FactionFiles Discord (link below)

## TODO

A lot of functionality is not implemented yet!

* version or build date in window header
* Support mods that modify files inside vpp_pc archives as ModManager did
* Support installing several mods at same time if they don't conflict with multiplayer
  * Allow to "void warranty" and install custom mix of multiplayer mods
  * Better UI: need some sort of ordered multi select with checkboxes and numbers (or something better)
* Extend dev mode to aid mod developers in their crusades
  * Fast switch between different game versions, mod builds, etc
  * Allow installing local patch/update/mod manually to help with testing (add file picker)
  * Button to clear caches
  * Button to open game directory
  * ???
* Check discord for feature requests
* Better logging: now it's a mess of important stuff, trash and bad-formatted information

## Tech info

Built using `dotnet 6`. UI is using WPF, core functionality is separated to a class library. Currently tied to Windows because of WPF and Registry (used to find game installation). Linux port only requires another UI or CLI.

Release is always single-file .exe with only dependency on dotnet framework installed (will ask to download if not).

All settings, cache, backups and downloads are stored inside `<game_dir>/.syncfaction`. Folder can be removed at any time.

## Credits

Implemented with support from **moneyl, Camo, Goober, natalie, ATMLIVE** and others from FactionFiles community. You people are awesome, bringing life to my favourite game! <3

([join Discord!](https://discord.gg/factionfiles))
