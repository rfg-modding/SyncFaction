# Modding/Verification

> Advanced feature!
>
> It doesn't really have a use-case for regular mods. Intended for Terraform patch and RSL developers.

If you include a `.mod.hashes.json` file in your mod root, SyncFaction will calculate hashes of files after mod is applied and compare to hash values you provided.

## Warning

There is absolutely no point in doing that for regular mods. Imagine this situation:

* user wants to apply several mods
* these mods edit same files and expect certain hashes
* verification will fail for every subsequent mod
* user won't be able to use these mods together

Purpose of this feature is to ensure that everyone has exactly same files after installing Terraform Patch and RSL, which are installed automatically and are required to play multiplayer. If files were to end up different, multiplayer won't work for you and can crash lobby for everyone.

## Details

Place a file named `.mod.hashes.json` in mod root. It should contain JSON dictionary with file => hash paris with files you want to be verified.

* Hash algorithm is `SHA256`
* Hash value is HEX UPPERCASE string with no delimiters
* Filenames are relative to game root, eg `rfg.exe` or `data/misc.vpp_pc`
* Files not mentioned in hash list won't be checked
* Failed verification fails mod installation

Tip: no need to write this file yourself. Install your mod and generate diagnostics report in SF. You'll have all file hashes in a slightly different format, just copy ones you need and remove extra data.

## Example

> `mod_root/.mod.hashes.json`

```json
{
  "data/chunks.vpp_pc": "5C2C1D7A62E9BD1D52815918B0ACB67ECDF7EFC05AFCD46F3B91F4BF671A1973",
  "data/effects.vpp_pc": "6277E9DF4D471F32C1BF7A7A18265EAAE41423366C43F9E7B3D07D33190B4EBA",
  "data/effects_mp.vpp_pc": "CE8E3B09F1EAE980F0CCBA5536D8AF148300899CA3091B4FEFB8981256C86937",
  "data/terr01_precache.vpp_pc": "FEE2544B55F0FD6F642A23BBAEADBEFD9D0FB76B05656F591A2E292C6A759590",
  "data/vehicles_r.vpp_pc": "8411B3C42249458CD51741A348ED1B8713B2EC4652D4EFFFE199BC88E2D4753F",
  "Galaxy.dll": "BAFEB03CA094E95226B4992314B15118C54F582DA3C4B0401C59C92C3F472191",
  "GalaxyPeer.dll": "9BFD8835020EF832001C7893DF070AF1F110D5BEEBF86E87B6133665C5329590",
  "Icon.ico": "8EB782B088D0A456A0C474CDB350DDFEC2711881524614C69DF1ACC67616FCCC",
  "rfg.exe": "7A82D2D0F425AF5E75D8FFBCE12FAC53EB5CA9CD812731CCF5A29697E906AF0E",
  "thqnocfg_gog.dat": "6F0427B331306C823AFDC21347366C822645A6EEA4C64D241BBE9E29DE7E0C1D",
  "data/terr01_l1.vpp_pc": "12CC145B96266F1BC70FE969197AE7022E3DACA448BF59D0C60B8D56F4DE7848",
  "sw_api.dll": "67F9F8E976157EBF6E8EA2FC93681A102F513FEFD11998273EEB8DA743CBE71F"
}
```

You'll see something like this in SF output if hash file is present in mod:

```
Applying mods
Restoring files to latest patch
Installing 7728369504083097443 GOG Unification Patch.zip
•	Copied .hashes.json to .hashes.json
•	Copied sw_api.dll to sw_api.dll
•	Replaced 0h_c1218.str2_pc in terr01_l1
•	Replaced 0h_c1618.str2_pc in terr01_l1
•	Replaced 0h_c1219.str2_pc in terr01_l1
•	Replaced terr01_l1.asm_pc in terr01_l1
•	Patched files inside data\terr01_l1.vpp_pc
Verifying: 12 files
•	Verifying: 10/12, 0:02 left
•	Verifying: 11/12, 0:00 left
Verifying: 12/12 files, completed in 0:30
Applying mods: Done

```