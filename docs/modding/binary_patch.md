# Modding/Binary Patch

> Advanced feature!
>
> It doesn't really have a use-case for regular mods. Intended for Terraform patch and RSL developers.

If you want to slightly edit a large file and want to save some bandwidth, you can create a binary patch and SF will apply it.

## Warning

There is absolutely no point in doing that for regular mods. Imagine this situation:

* user wants to apply several mods
* these mods edit same files
* patching will fail for every subsequent mod
* user won't be able to use these mods together

Purpose of this feature is to minify download and install time of Terraform Patch and its incremental updates. Also, it performs hash checks under the hood, so patched files are guaranteed to be the same every time. If files were to end up different, multiplayer won't work for you and can crash lobby for everyone.

## Details

* Get yourself a copy of `xdelta3-3.1.0-x86_64.exe` from [xdelta-gpl](https://github.com/jmacd/xdelta-gpl/releases) project
* Create a copy of a file you are going to edit
* Make edits to a file
* Run `xdelta3-3.1.0-x86_64.exe -e -S -s file.original file.edited file.xdelta`
* Place `file.xdelta` in your mod

> `-S` flag is important! It disables secondary compression because it's not supported in C# xdelta decompressor used by SF.

Filename and path should match name and path of the file you want to patch. Keep an eye for extension too, it must be `.xdelta` and replace last part of original file's extension. See examples!

Caveat: sometimes xdelta patches are too big or take a lot of time to install and this defeats the purpose. You have to test and see how it goes for your edits.

## Example

| game file | xdelta file |
|-|-|
| game_root/rfg.exe | mod_root/rfg.xdelta |
| game_root/data/misc.vpp_pc | mod_root/data/misc.xdelta |
| game_root/foo/bar/test.a.b.c | mod_root/foo/bar/test.a.b.xdelta |
