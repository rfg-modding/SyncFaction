# Dev Mode

Features for advanced users and developers. To enable, click the `Dev mode` checkbox.

## Diagnostics

If you have any issues and want to *help us help you help us all*, press `Generate Report` and wait. SF will collect information about your game files and mods, SF logs and settings, and print it all to a textbox nearby. Click `Copy to Clipboard` and paste it somewhere, eg in Github Issues or in relevant Discord channel. If you are paranoid, paste it to Notepad first and see what's included in the report. Basically it is:

* Information about all files and directories inside `game_root`
* SF state and settings: applied mod list, checkboxes, values you chose for mods with their own settings, ...
* Details about last exception (that orange exclamation sign you see if something goes wrong)
* SF logs, very detailed. Not only that text you see in app window, but also every decision, file operation, network activity, etc. This includes full paths so may disclose your folder structure or windows username if you installed your game eg. in `C:\Users\username\Documents`. Typically game is installed in `C:\Program Files` so this is not an issue

## Settings

* `Multithreading`: uncheck to make certain operations single-threaded if your PC lags while SF works with files or network
* `Use CDN`: **some** files from FF are mirrored to SyncFaction CDN. If a file is not mirrored to SF CDN, it is downloaded from FF anyway. If your downloads are slow, uncheck and see if there is any improvement. Maybe downloading directly from FF works better for you.

## Restore

To quickly switch between heavliy-modded and clean MP-compatible game, there are 3 buttons:

* `Restore to Patch`: removes any mods and cleans up game to the latest Terraform update. Use this to join MP
* `Restore Last Mods`: applies mods you have installed before. Use this to get back to your setup after playing MP
* `Restore to Vanilla`: *it's gonna take you back to the past*, to play original game without any edits

These all work by copying files from backups in `game_root/.syncfaction/.bak*`. That's why SF needs a lot of space to work.

## Dev Options

Be careful, these options tamper with SF logic and will break your warranty because SF is designed to provide certain experience, eg. keep you updated automatically.

* `Startup update check`: if disabled, SF will not check for updates, read mods and news from FF automatically when started. Basically makes SF offline-first. `Refresh` button still works if you want to download something, but it won't check for updates
* `Open directories` is a quick way to access game files in Explorer
  * `<root>`: open game directory
  * `/data`: open game data directory
  * `/.syncfaction`: open app directory

Things not intended for users:

* `Dev and hidden mods`: if enabled, will show mods from SF CDN. Intended for modders to quickly share dev builds. Also will display hidden mods, eg. installed updates
* `UI: Update required`: uncheck to bypass update nag
* `UI: Interactive/Busy`: toggle locked buttons and "working" state on/off
* `Repeat App Initialization`: perform checks and actions done on app startup. Used for debugging
* `Switch Theme`: toggle light/dark theme. Used for debugging
* `Get Logs and Clear Output`: displays logs and removes them from memory, clears text window
