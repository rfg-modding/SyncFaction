[![Build status](https://ci.appveyor.com/api/projects/status/pe6ee7xn6rgqn2pm/branch/master?svg=true)](https://ci.appveyor.com/project/Moneyl/rsl/branch/master)

# Re-mars-tered Script Loader
RSL is a lua scripting interface and modding tool for Red Faction Guerrilla Re-mars-tered. The main goal of this project is to greatly expand what mods can do to the game, and what they can change, and to improve the mod development experience. Please see the [docs](https://rsl.readthedocs.io/en/latest/) for much more info about the project, the features it provides, and how to contribute to it. See the [Official Red Faction Discord Server](https://discord.gg/wYT7pj5) to talk with the devs, other modders, and RF fans.

You can find the latest builds [here](https://ci.appveyor.com/project/Moneyl/rsl/history).
You can find the installation guide [here](https://rsl.readthedocs.io/en/latest/Installation.html).

## Contents
- [Project goals](https://github.com/rsl-dev/RSL#project-goals) 
- [Bug reports and feature requests](https://github.com/rsl-dev/RSL#bugs-and-feature-requests)
- [Examples](https://github.com/rsl-dev/RSL#gallery) 
- [Libraries used](https://github.com/rsl-dev/RSL#libraries-used)

## Project goals
Normally, modding RFGR involves editing xml files included in the game which define things such as AI behavior, the properties of vehicles, weapons, and more. This allows modders to change the game in many ways. Even so, there are many hardcoded values and behaviors that cannot be changed, ultimately limiting what modders can do. This tool aims to bypass some of these limitations by allowing modders to change many hardcoded values and giving them access to the power and flexibility which a scripting languages allow for.

Since this involves manipulating many parts of the games code and memory, this will likely expand to other modding features over time, such as replacing textures and other files without unpacking any vpp_pc files.

See the [docs](https://rsl.readthedocs.io/en/latest/) for info on how to install and use the script loader, description of the scripting API and script examples.

## Bug reports and feature requests
See the [issues](https://github.com/rsl-dev/RSL/issues) section and search for an issue which already describes your bug/feature/question. If you can't find it, create a new one.

## Examples
![alt text](https://github.com/rsl-dev/RSL/blob/master/Images/0.5.0-GuiExample1.jpg "Main overlay from 0.5.0")
Above is an image of the overlay provided by the RSL in version 0.5.0. The overlay includes useful features such as a script editor, and explosion spawner, a teleport menu, and some preset tweaks which can be useful such having AI ignore the player, or invulnerability. These can be useful for fun, and also when testing out mods or trying to learn more about how the game works.

### Scripting examples
Below are some examples of the scripting API as of version 0.5.0. Please see the [scripting intro](https://rsl.readthedocs.io/en/latest/Scripting/Guides/Introduction.html) and the other guides and examples in the docs for more info.

```lua
HumanCount = 0
for i=0, rfg.ActiveWorld.AllObjects:Size(), 1 do --Loop through the global object list
    CurrentObject = rfg.ActiveWorld.AllObjects[i] --Make a reference variable to the current object for convenience.
    if CurrentObject.Type == rfg.ObjectTypes.Human then --Check if current object is a human object
        HumanCount = HumanCount + 1
    end
end

rsl.Log("HumanCount: {}\n", HumanCount)
```
The above example shows how you can loop through the games global list of objects, filter them by type, and interact with them. The next example shows a few other things that can be done.
```lua
rfg.TeleportPlayer(1434.0, 15.0, 693.3) --Teleport the player to the Oasis safehouse
rfg.ExplosionCreate(rfg.GetExplosionInfo("remote_charge"), 1434.0, 25.0, 693.3) --Create a remote charge explosion 10m above the player
rfg.SetGravity(0.0, -3.7, 0.0) --Set the gravity to actual mars gravity (default is -9.82)
rfg.SetAlertLevel(rfg.AlertLevels.Red) --Set the alert level to red
```
## Libraries used
[Dear ImGui](https://github.com/ocornut/imgui) - Used for the in game overlay GUIs.

[Sol 2](https://github.com/ThePhD/sol2/) - Used for quick and easy lua binding.

[LuaJIT](https://luajit.org/) - A JIT compiled version of Lua. Used as the primary language of the scripting API.

[Minhook](https://github.com/TsudaKageyu/minhook) - Used for function hooking.

[Kiero](https://github.com/Rebzzel/kiero) - Used to aid in locating directx functions for hooking.
