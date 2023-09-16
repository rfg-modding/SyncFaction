# Usage

First, some abbreviations:

* SF = SyncFaction app
* FF = [FactionFiles](https://factionfiles.com), community site for all content related to RedFaction series
* MP = multiplayer mode
* SP = singleplayer campaign
* WC = wrecking crew singleplayer mode
* RFG = the game, 2018 remastered version
* Terraform aka Community patch = [project by Camo](https://github.com/CamoRF/Red-Faction-Guerrilla-Terraform-Patch) fixing game bugs, rebalaning MP, ...
* Reconstructor, RSL/RSL2, Script Loader = [extension by moneyl](https://github.com/rfg-modding/Reconstructor) to provide extra moddability


## First run

If you placed `SyncFaction.exe` inside game directory, it will detect current game directory. If you have single GOG or Steam version installed, SF will look them up automatically, no matter where SF exe is placed. If you have **both** game versions or don't have relevant registry entries, SF won't be able to make a decision for you and will ask to locate game manually **every time**.

On first run, SF checks your game files. If you had mods before or tampered with game files - you have to revert all of that. Or just verify your installation with Steam or GOG Galaxy.

SF will detect if your desktop is light or dark and use according theme automatically.

If you have Steam version, SF will ask if you want to copy your savegame to location where GOG version expects it. This is done only once and is required because the game will be converted to GOG version when you update it. Savegames are always backed up in destination directory.

Savegame files are:

* Steam: `C:\Program Files (x86)\Steam\userdata\STEAMUSERID\667720\remote\autocloud\save\keen_savegame_0_0.sav`
* GOG: `C:\Users\USER\AppData\Local\GOG.com\Galaxy\Applications\51153410217180642\Storage\Shared\Files\autocloud\save\keen_savegame_0_0.sav`

## Updates

SF checks for updates of Terraform Patch and Reconstructor and asks user to download and install them. They are absolutely required to play multiplayer and provide common ground for future mods. Normally you don't need to avoid or disable auto-update. If, for some reason, you want app to work entirely offline, want to play vanilla game or apply legacy mods that don't work on top of Terraform - read further.

SF will never check for its own updates or auto-update itself, nor will it upload any information/telemetry.

## Downloading mods

After update check, SF reads mod list from FactionFiles and displays news page. Again, this can be disabled if you are not interested in multiplayer or RFG community news.

To download and unpack mods from FF, select mods from right panel and press `Download`. They will become available on the second tab.

You can also add mods manually. Mods must be normal directories, not archives! Place them in `game_root/data/.syncfaction`

`Refresh` button updates online mod list if you moved some folders or want to get list from FF again. It also reloads news page.

## Mod management

Once you have some mods downloaded or copied, switch to `Install` tab. Select mods you want to enable and drag them all the way up to `Enabled` category. Click mod again to deselect. You can also reorder items by dragging them. Order is important because mods can edit same files and overwrite each other's edits.

When you select a mod, you'll see its description and probably some warnings regarding compatibility with multiplayer or other mods. Please read these carefully, it may help you find the right order if you experience issues.

Some mods have settings. A panel will appear in the middle to let you select values provided my mod author.

When you are ready, press `Apply` and wait. **In case of errors, game state is not automatically cleaned up**, so you will need to undo some mods and try to `Apply` again. If you hit `Apply` with 0 mods, game is effectively cleaned up to latest patched state.

`Refresh` button updates local mod list if you moved some folders.