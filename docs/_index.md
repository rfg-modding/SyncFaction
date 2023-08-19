# SyncFaction

This is main documentation page with general info. See sidebar links for [usage](usage.md), [troubleshooting](troubleshooting.md), [modding](modding/intro.md), etc

## Installation

Download [latest release](https://github.com/rfg-modding/SyncFaction/releases) and place .exe in game folder. That's it!

> You will need at least **40GiB** of free storage space

## Features

* Works for both Steam and GOG versions or **RFG Re-MARS-tered**
* Auto-update
  * Terraform patch (general SP game improvements, MP rebalance, tons of fixes, new maps)
  * RSL2 (script loader to fix crashes and enable more modding capabilities)
  * Incremental updates supported
* Integration with [Faction Files](https://www.factionfiles.com/ff.php?action=files)
  * Browse, download and install mods
  * See news for upcoming Game Nights
* Download progress display, cache and resume on errors for places with bad internet
* Backup management: no need to download game files over and over again if something is messed up
* Fast switch between game versions
  * `patched` to play MP without worrying about mod compatibility
  * `modded` to play SP with any mods even if they affect MP
  * `vanilla` if you want to roll back in time
* Mods can be placed in app directory manually, old-school way
* CDN for faster downloads and sharing dev builds
* Savegame transfer between game versions

## Non-features

* Saving storage space is not a priority. Backups are created only when files are modified for the first time, but after extensive modding game folder can be 2x heavier (or more!) because every game file is stored twice for quick restore
* Same for saving IO. Every time you change mods, files are restored to latest patch and all mods are re-applied from scratch. Complex IO-friendly file tracking would be difficult to get right
* Editing game file formats for modding is done manually with Nanoforge and other tools. Only VPP archives are supported for now in SF

## Continue Reading

* [Usage](usage.md)
* [Troubleshooting](troubleshooting.md)
* [Modding Intro](modding/intro.md)