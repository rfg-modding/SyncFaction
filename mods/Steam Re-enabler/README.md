# Steam Re-enabler

Mod consists of 4 files:

* `rfg.xdelta` is a binary diff to revert GOG .exe back to steam .exe
* `sw_api.xdelta` reverts sw_api from GOG-patched .dll to steam .dll
* `dinput8.dll` 
* `Launcher.exe` is a no-op thing which now opens steam url and is required 

All files are in a `.steam` folder, so SyncFaction will only apply them if game is known to be installed from Steam. Otherwise, mod does not affect any files and installation fails.

## dinput8.dll

It is a replacement for nag dll from Reconstructor. SyncFaction can't delete files, so it has to be replaced to disable nag message. I commented out this block of code and it just acts as transparent proxy and loads normal `dinput8.dll` without any interventions.

```cpp
/*
LPSTR commandLineArgs = GetCommandLineA();
BOOL containsArg = (strstr(commandLineArgs, "/RanWithReconstructor") != NULL);
if (containsArg == 0)
{
    MessageBoxA(NULL, "Please run the game using SyncFaction or Launcher.exe.\r\nIf you run it directly or through Steam/GOG you won't have important bugfixes and improvements required by modern RFG mods.\r\nThe game will now close.", "Game not launched correctly", MB_OK);
    ExitProcess(0);
return;
}
*/
```

## Launcher.exe

This is a replacement for launcher from Reconstructor. SyncFaction uses Launcher.exe if it exists, and we can't delete this file. Also, since we restore everything to Steam version, we can't run rfg.exe either. So this version of Launcher calls `shell open` with steam URL to run the game. I did not bother to write down what i changed in source code, unfortunately