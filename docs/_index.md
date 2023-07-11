# SyncFaction

This is main documentation page with general info. See sidebar links for more detailed topics.

## Installation

Download [latest release](TODO) and place .exe in game folder. That's it!

> You will need at least **20GiB** (TODO measure with terraform rebuild) of free storage space

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
  * `vanilla` if you want to roll back in time
  * `patched` to play MP without issues
  * `modded` to play SP with any mods even if they affect MP
* Mods can be placed as regular folders manually, old-school way
* CDN for faster downloads and for sharing dev builds

## Non-features

* Saving storage space is not a priority. Backups are created only when files are modified for the first time, but after extensive modding game folder can be 2x heavier (or more!) because every game file is stored twice for quick restore
* Same for saving IO. Every time you change mods, files are restored to latest patch and all mods are re-applied from scratch. Complex IO-friendly file tracking would be difficult to get right
* Editing game file formats for modding is done manually with Nanoforge and other tools. Only VPP archives are supported for now in SF

## Getting help

### App startup

If app does not start and leads you to Microsoft downloads page, you need the

> [x64 .NET 6.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime) for **DESKTOP APPS**

That page can be confusing, see the screenshot:

![dotnet desktop runtime](https://user-images.githubusercontent.com/1562341/204090216-2d163e9c-b60e-4e45-88e2-bcd4f21aab69.png)

### General issues

* Use Steam or GOG Galaxy to check integrity of game files
* Check if game location is valid
* See if new versions of SyncFaction are available on Github
* Please report errors here in issues. **Create and copy diagnostics report** to help fixing it! Also describe what you were trying to do.
* Probably ping **rast1234** on FactionFiles Discord (link below)

