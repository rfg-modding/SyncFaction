# Modding/Version Specific Mods

> Advanced feature!
>
> It doesn't really have a use-case for regular mods. Intended for Terraform patch and RSL developers.

Allows you to edit/replace files only on certain game version, Steam or GOG

## Warning

There is absolutely no point in doing that for regular mods. Terraform Patch unifies both versions and you can think of them as completely equivalent.

Purpose of this feature is to allow Terraform Patch apply specific edits to Steam version and not touch GOG version. It would be a nightmare to support MP patch and SP mods for two different game versions.

## Details

* place GOG-specific files to `mod_root/.gog/`
* place Steam-specific files to `mod_root/.steam/`

File replacements and xdelta binary patches work fine. Directory structure must reflect game structure.

Caveat: modinfo.xml may or may not work with this approach, needs testing!

## Example

| game file | edit | effect |
|-|-|-|
| game_root/rfg.exe | mod_root/.steam/rfg.xdelta | patch will be applied only to Steam version |
| game_root/data/misc.vpp_pc | mod_root/.gog/data/misc.vpp_pc | file will be replaced only in GOG version |
