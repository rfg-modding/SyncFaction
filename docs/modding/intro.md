# Modding/Intro

Welcome to documentation for modders! These pages will guide you through mod development and distribution process. However, i won't describe in detail how game works, how resources are structured etc. There is [FactionFiles Wiki](https://www.redfactionwiki.com/wiki/RF:G_Editing_Main_Page) entry for that.

There are several ways to make a mod. Here's quick overview. Even more details can be found on separate pages, see navigation on the left.

When you are done, upload your mod to FactionFiles in RFG general mods category, ask on Discord to approve it, and it will be downloadable right from SyncFaction!

## Mod structure

SF expects a mod to be a directory with files in `<game_root>/data/.syncfaction` or, when downloading from FF, an archive without intermediate directory inside. Here's an example:

```

📂 <game_root>/data/.syncfaction/ultrafast_cars  // ✅
|-📄 rfg.exe
|-📂 data
  |-📄 vehicles_r.vpp_pc

📂 <game_root>/data/.syncfaction/slow_cars
|-📂 slow_cars  // ❌
  |-📄 sw_api.dll
  |-📂 data
    |-📄 misc.vpp_pc

📦 fast_walkers.zip  // ✅
|-📄 libeay32.dll
|-📂 data
  |-📄 table.vpp_pc

📦 slow_walkers.zip
|-📂 slow_walkers  // ❌
  |-📄 binkw32.dll
  |-📂 data
    |-📄 skybox.vpp_pc
```

> Be careful when you pack your mod: if you do it by right-clicking a directory and `add to archive`, you'll get that annoying extra directory inside the archive!

Now let's see how you can share modded files.

## File replacement

> Not recommended!

Let's say you changed something in game files: patched `.exe`, or modified something inside `.vpp_pc` archives. You can share it all right away. Directory structure in your mod must be the same as in the game. Example:

```
📦 powerful_turrets.zip
|-📄 rfg.exe
|-📂 data
  |-📄 misc.vpp_pc
  |-📄 table.vpp_pc
  |-📄 vehicles_r.vpp_pc
|-📂 new_folder
  |-📄 something.else
```

Pros:

* The most simple way to share your work

Cons:

* It will not be compatible with other mods: either you will overwrite files or your files will be overwritten eventually

## Loose archives

Useful for distributing modified contents of a `.vpp_pc` archive. Create a folder named as the archive you want to patch and place files there. Existing archive entries will be replaced. New ones will be added after existing entries inside the archive.

```
📦 weak_turrets.zip
|-📂 data
  |-📂 terr01_l1.vpp_pc
    |-📄 0h_c1218.str2_pc
    |-📄 terr01_l1.asm_pc
    |-📄 new_file
  |-📂 misc.vpp_pc
    |-📄 tweak_table.xtbl  // please don't use this method for XTBLs! read below for a better approach
```

To delete archive entries, create a file `.delete.json` with an array of names. Example:

> `mod_root/data/misc.vpp_pc/.delete.json`

```json
[
    "dlc_marauder_charge.rig_pc",
    "hess_helmet.rig_pc"
]
```

To change archive options, create a file `.options.json`. Example with currently supported options:

> `mod_root/data/misc.vpp_pc/.options.json`

```json
{
	"Mode": "Normal" // Archive compression aka VPP Flags: "Normal", "Compressed", "Condensed", "Compacted" (both compressed and compacted)
}
```

Pros:

* Mods made this way will be more compatible with each other (as long as they affect different files)

Cons:

* For `.xtbl` and other XML-based files it is better to use more advanced approach (see below) because a lot of mods like to edit the same files.

## ModInfo.xml

Based on original ModManager file format with some fixes and improvements. It is best suited for mods that edit XML files (`.xtbl .dtdox .gtodx`) and you also can provide configurable options with it.

> Description and examples for the format are on their own page because they are quite long: [ModInfo](modinfo.md)

Pros:

* Mods made this way will be most compatible with each other, because they edit only relevant parts of XML files without replacing them entirely
* Can replace archive entries as files, not only patch XMLs
* User inputs! You can let players choose values your mod is going to change: weapon damage, FOV, gravity strength, what file should be used, etc

Cons:

* Difficult to understand if you are new to RFG modding

## Mixed

Nothing stops you from building a mod where all of these approaches are used together. However, be careful because SF is not tested on all possible combinations, especially when you have `modinfo.xml`. Please test how your mod installs. If you see inconsistent or weird behavior, report a bug.

## Testing

Mods from FF are downoladed to `<game_root>/data/.syncfaction/Mod_id` folders. Similarly, you can test how your mod works by placing all the files in `<game_root>/data/.syncfaction/Your_mod_name`. It will be shown in local mod list in SF and you can try to apply it. Also you are free to edit any files in downloaded mods and see how they affect your game. Just remember to apply mods every time you make changes.

## Compatibility

SF is designed to be compatible with as many existing mods as possible. It has logic to guess file locations but **please don't rely on it**. Examples:

```
📦 fast_tanks.zip
|-📄 rfg.exe
|-📄 misc.vpp_pc  // SF will guess it's meant to be inside data
```

```
📦 slow_tanks.zip
|-📂 slow_tanks
  |-📄 rfg.exe  // SF will guess it's meant to be inside game_root
  |-📄 misc.vpp_pc  // SF will guess it's meant to be inside data
```

ModInfo mods usually reference `.vpp_pc` archives as `.vpp`. Also, old mods for RFG Steam Edition (non-remastered) reference archives in a folder other from `data`. SF fixes these inconsistencies, so in theory old mods could work. But game may have changed in remastered version and while old mods are installable, they may not work properly.

SF will also sync edits made by `modinfo.xml` between `table.vpp_pc` and `misc.vpp_pc`. This is because ModManager just deleted one of these files and all mods were made to edit only another file. This is bad for a number of reasons so as a fix SF will apply same edits to both of them.
