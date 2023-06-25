namespace SyncFaction.Core.Services.FactionFiles;

public static class ModExtensions
{
    public static string InfoMd(this IMod mod) => string.Join("\n\n", EnumerateFlags(mod));

    private static IEnumerable<string> EnumerateFlags(IMod mod)
    {
        if (mod.Flags == ModFlags.None)
        {
            yield return "> * Mod has no known files and probably will do nothing";

            yield break;
        }

        if (mod.Flags.HasFlag(ModFlags.AffectsMultiplayerFiles))
        {
            yield return "> * Warning: mod will edit **multiplayer files**. If you experience issues playing with others, disable it";
        }

        if (mod.Flags.HasFlag(ModFlags.HasXDelta))
        {
            yield return "> * Mod has binary patches. It will only work on files in certain state, eg. when they are unmodified. If you experience issues, try to change mod order, placing this one before others";
        }

        if (mod.Flags.HasFlag(ModFlags.HasModInfo))
        {
            yield return "> * Mod is in ModManger format. If it has user-defined settings, they are on the right. Some ModManager-style mods can be incompatible with one another or overwrite each other's edits";
        }

        if (mod.Flags.HasFlag(ModFlags.HasReplacementFiles))
        {
            yield return "> * Mod replaces certain files entirely. If they were edited by other mod before, changes will be lost. If you experience issues, try to change mod order, placing this one before others";
        }
    }
}
