# RSL Legacy Collection

This is a compilation of all existing RSL-based scripts and related tweaks. Since RSL is not being developed anymore, and its successor, Reconstructor, has a long way to go before scripting is available again, i decided to wrap all community efforts into one convenient package.

These mods/scripts are rough and unpolished, feel free to edit scripts to your liking!

Based on works by multiple authors, including moneyl, Camo, arrows, RFG, donslockz, Hazard, VAXIS. Compiled together, tested and refined by rast1234.

## Before you start

* Works ONLY with Steam version of RFGR
* This mod disables Reconstructor
* [Visual Studio 2019 x86 Redistributable](https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads/) is required. You might need to reboot
* Disable overlay software. Recommended to disable Steam overlay too, even if it appears to work

## RSL

Check out docs at https://rsl.readthedocs.io . Basic controls:

* F1 opens self-explanatory overlay UI
* Some scripts are auto-loaded, others should be loaded manually
* All scripts show an in-game message when loaded. Check console for logs or errors if not sure
* More at https://rsl.readthedocs.io/en/latest/OverlayGuide.html

## First person mode

by moneyl

It is not a script, it's a setting in RSL. `F1 (overlay) > Top menu > Tweaks > Camera settings > Toggle first person camera`

You will want to tune `Camera offset` and FOV in game settings to get better results.

To hide Mason's head and avoid clipping through it, use mod settings in SyncFaction. This is not ideal because some NPCs will have creepy faces.

# Flyer controls

by [moneyl](https://discord.com/channels/416631942738346008/424009433253806080/1192943648690675913), [RFG & donslockz](https://discord.com/channels/416631942738346008/1000693872256614453/1065268322960162846)

Script that lets you "control" the flyer. You can't use flyers that come with high alert level as they're piloted by NPC. Use mod settings in SyncFaction to spawn flyers at safehouses. Also there is an improved version with pitch and roll controls, but flyer catches fire if you tilt it too much. Controls:

* `V`: teleport to flyer if you are aiming at it. Works from any distance
* `WASD`: move
* `Shift/Ctrl`: fly up/down
* `Z/X`: change amount of force applied by controls
* `E`: exit, as with vehicle
* `arrow keys`: roll/pitch, in improved script only

# Telekinesis

by [moneyl](https://www.nexusmods.com/redfactionguerrillaremarstered/mods/19)

> This mod autoloads on startup. To disable, rename or delete its `main.lua` file.

Lift objects with the power of your mind! Throw EDF vehicles off cliffs, or through buildings. Use debris and other heavy objects as weapons. Controls:

* `T`: grab object. Note that some objects can't be moved by telekinesis at the moment
* `Q/E`: change strength by 10. Hold ctrl while pressing q or e to change strength by 100
* `F`: throw the object you're currently lifting in the direction you're aiming
* `Ctrl + mouse wheel scroll`: change distance to object


# Graphics

by [Camo](https://www.factionfiles.com/ff.php?action=file&id=6364)

> This mod autoloads on startup. To disable, rename or delete its `main.lua` file.

A lot of improvements based on internal engine parameters, impossible with other tools like ReShade or Nvidia profiles. How to use:

* Load savegame
* Disable shadows in game settings, apply
* Set shadows back to HIGH, apply
* Graphics will be improved until you exit the game

# Physics

by [VAXIS](https://www.factionfiles.com/ff.php?action=file&id=7503) and [Camo](https://www.factionfiles.com/ff.php?action=file&id=6363)

* VaxisFrictionMod with several presets to change building toughness
* Gravity presets to load by default or manually. Rename `_main.lua` to `main.lua` to enable auto load

# Vehicle Overlay

by Camo and moneyl

Displays vehicle information. Useful for debugging mods. Activate by executing script. Can be disabled with `RemoveLabels()` function, though it will interfere with other scripts if they display text

# Teams

by [Camo](https://www.factionfiles.com/ff.php?action=file&id=7502) and [arrows](https://www.factionfiles.com/ff.php?action=file&id=4699)

Scripts that let you change player or NPC team: Guerrilla, EDF or Marauder. `Set Team` scripts are one-shot, `EveryoneHostile` works even on newly spawned NPCs

# No overheating

by [arrows](https://www.factionfiles.com/ff.php?action=file&id=4707)

Removes overheating from all weapons, including turrets
