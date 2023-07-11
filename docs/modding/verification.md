# Modding/Verification

> Advanced feature!
>
> It doesn't really have a use-case for regular mods. Intended for Terraform patch and RSL developers.

If you include a `.hashes.json` file in your mod root, SyncFaction will verify hashes of files after mod is applies and compare to hashe values you provided.

There is absolutely no point in doing that for regular mods. Imagine this situation:

* user wants to apply several mods
* these mods edit same files and expect certain hashes
* verification will fail for every subsequent mod because
* user won't be able to use these mods together

Purpose of this feature is to ensure that everyone has exactly same files after installing Terraform Patch and RSL, which are installed automatically and are required to play multiplayer. If files end up differently, whole multiplayer lobby can crash.

## Details

* Hash is `SHA256`
* Filenames are relative to game root, eg `rfg.exe` or `data/misc.vpp_pc`
* Files not mentioned in hash list won't be checked
* Failed verification fails mod installation

Tip: no need to write this file yourself. Install your mod and generate diagnostics report in SF. You'll have all file hashes in a slightly different format, just copy ones you need and remove extra data.

## Example

> `mod/.hashes.json`

```json
{
    TODO
    "": "",
    "": "",
}
```