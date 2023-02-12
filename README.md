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

* button to report current state, collect log and probably list files/hashes
* sanity checks for state management, eg when resetting to vanilla (now app needs restart)
* Check discord for feature requests
* Better logging: now it's a mess of important stuff, trash and bad-formatted information
* rickroll
* COMMUNITY PATCHES MUST SUPPORT MODINFO XML
* Support mods that modify files inside vpp_pc archives as ModManager did
  * display inputs from MM
  * account for situations when modinfo.xml is inside subfolder
  * show resulting XML tree after user input
  * apply operations
    * unpack
    * edit XML contents
    * move files
    * pack
    * save/load state of selected values and custom inputs
    * SameOptionsAs must be copied before displaying
    * display list of files changed by mod (even for non-xml mods)
* compare hashes for steam and gog versions
* test what happens if cdn is unavailable
* show warnings if mod edits MP files. also show files to be modified as list
* rename community patch to terraform patch where possible
* if both .reg versions are detected, fail auto search and ask to place app in game dir

## Tech info

Built using `dotnet 6`. UI is using WPF, core functionality is separated to a class library. Currently tied to Windows because of WPF and Registry (used to find game installation). Linux port only requires another UI or CLI.

Release is always single-file .exe with only dependency on dotnet framework installed (will ask to download if not).

All settings, cache, backups and downloads are stored inside `<game_dir>/.syncfaction`. Folder can be removed at any time.

## Credits

Implemented with support from **moneyl, Camo, Goober, natalie, ATMLVE** and others from FactionFiles community. You people are awesome, bringing life to my favourite game! <3

([join Discord!](https://discord.gg/factionfiles))


0. pause XML merging features (that's the heart of modmanager)
1. introduce VPP repacking support for mods/updates
2. rewrite updater
   * store terraform and rsl updates in separate lists
   * apply terraform, then rsl
   * track a list of extra files and delete them on restore
     * restore to vanilla = delete all extra files
     * restore to community = should keep files introduced by community patch
       * probably OK to delete all extra files and then copy all from community backup
3. track new separate set of search strings from FF - for RSL2
4. compare gog and steam distributions
5. release unification patch based on observations and VPP differences
6. all this stuff will require rebuilding current state of game files from ground up, but since i have cache and backups it won't be a problem for users
7. people will need to get new SF version though
   i'd like to make things smooth and will need help testing...
