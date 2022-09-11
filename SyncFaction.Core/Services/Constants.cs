namespace SyncFaction.Core.Services;

public static class Constants
{
    public const string BakDirName = ".bak_vanilla";
    public const string CommunityBakDirName = ".bak_community";
    public const string AppDirName = ".syncfaction";
    public const string StateFile = "state.txt";
    public const string ApiUrl = @"https://autodl.factionfiles.com/rfg/v1/files-by-cat.php";
    public const string FindMapUrl = @"https://autodl.factionfiles.com/findmap.php";
    public const string BrowserUrlTemplate = "https://www.factionfiles.com/ff.php?action=file&id={0}";
    public const string WikiPage = "https://www.redfactionwiki.com/wiki/RF:G_Game_Night_News";

    public static readonly string ErrorFormat = @"# Error!
**Operation failed**: {0}

##What now:
* Use Steam to check integrity of game files
* Check if game location is valid
* See if new versions of SyncFaction are available on Github
* Please report this error to developer. **Copy all the stuff below** to help fixing it!
* Take care of your sledgehammer and remain a good Martian

{1}

";

}


/*



VPPs used by MP & SP:
	misc.vpp_pc
	table.vpp_pc
	anims.vpp_pc
	decals.vpp_pc
	skybox.vpp_pc
	sounds_r.vpp_pc
	steam.vpp_pc

VPPs only used by SP (includes DLC SP campaign in Extras > DLC):
	effects.vpp_pc
	humans.vpp_pc
	interface.vpp_pc
	items.vpp_pc
	activities.vpp_pc
	missions.vpp_pc
	terr01_l0.vpp_pc
	terr01_l1.vpp_pc
	terr01_precache.vpp_pc
	vehicles_r.vpp_pc
	zonescript_terr01.vpp_pc

	DLC campaign only:
		dlc01_l0.vpp_pc - DLC campaign only
		dlc01_l1.vpp_pc - DLC campaign only
		dlc01_precache.vpp_pc - DLC campaign only
		dlcp01_activities.vpp_pc
		dlcp01_anims.vpp_pc
		dlcp01_cloth_sim.vpp_pc
		dlcp01_effects.vpp_pc
		dlcp01_humans.vpp_pc
		dlcp01_interface.vpp_pc
		dlcp01_items.vpp_pc
		dlcp01_misc.vpp_pc
		dlcp01_missions.vpp_pc
		dlcp01_personas_en_us.vpp_pc
		dlcp01_vehicles_r.vpp_pc
		dlcp01_voices_en_us.vpp_pc
		dlcp02_interface.vpp_pc
		dlcp02_misc.vpp_pc
		dlcp03_interface.vpp_pc
		dlcp03_misc.vpp_pc
		zonescript_dlc01.vpp_pc


VPPs only used by MP & wrecking crew:
	effects_mp.vpp_pc
	items_mp.vpp_pc
    mpdlc_broadside.vpp_pc
    mpdlc_division.vpp_pc
    mpdlc_islands.vpp_pc
    mpdlc_landbridge.vpp_pc
    mpdlc_minibase.vpp_pc
    mpdlc_overhang.vpp_pc
    mpdlc_puncture.vpp_pc
    mpdlc_ruins.vpp_pc
    mp_common.vpp_pc
    mp_cornered.vpp_pc
    mp_crashsite.vpp_pc
    mp_crescent.original.vpp_pc
    mp_crescent.vpp_pc
    mp_crevice.vpp_pc
    mp_damage_control.bik
    mp_deadzone.vpp_pc
    mp_defensive_packs.bik
    mp_demolition_mode.bik
    mp_destructive_packs.bik
    mp_downfall.vpp_pc
    mp_excavation.vpp_pc
    mp_fallfactor.vpp_pc
    mp_framework.vpp_pc
    mp_garrison.vpp_pc
    mp_gauntlet.vpp_pc
    mp_movement_packs.bik
    mp_overpass.vpp_pc
    mp_pcx_assembly.vpp_pc
    mp_pcx_crossover.vpp_pc
    mp_pinnacle.vpp_pc
    mp_quarantine.vpp_pc
    mp_radial.vpp_pc
    mp_recon_packs.bik
    mp_rift.vpp_pc
    mp_sandpit.vpp_pc
    mp_settlement.vpp_pc
    mp_siege_mode.bik
    mp_support_packs.bik
    mp_warlords.vpp_pc
    mp_wasteland.vpp_pc
    mp_wreckage.vpp_pc
	wc1.vpp_pc
    wc10.vpp_pc
    wc2.vpp_pc
    wc3.vpp_pc
    wc4.vpp_pc
    wc5.vpp_pc
    wc6.vpp_pc
    wc7.vpp_pc
    wc8.vpp_pc
    wc9.vpp_pc
    wcdlc1.vpp_pc
    wcdlc2.vpp_pc
    wcdlc3.vpp_pc
    wcdlc4.vpp_pc
    wcdlc5.vpp_pc
    wcdlc6.vpp_pc
    wcdlc7.vpp_pc
    wcdlc8.vpp_pc
    wcdlc9.vpp_pc

Unknowns:
	chunks.vpp_pc - I don't expect anyone to mod this file. It has a few files which as far as I can tell are placeholder buildings used by the game in error conditions. Unknown when/if MP/SP use it
	cloth_sim.vpp_pc - I'd assume MP also uses this but not 100% sure. We don't have any tools to edit the .sim_pc files in these anyway. Likely won't for a very long time.
	personas_de_de.vpp_pc - Not completely sure on these vpps. They have voice lines for characters, so they might be SP only. We don't have tools to edit the contents of these.
    personas_en_us.vpp_pc
    personas_es_es.vpp_pc
    personas_fr_fr.vpp_pc
    personas_it_it.vpp_pc
    personas_ru_ru.vpp_pc
	voices_de_de.vpp_pc - Same deal as the personas_xx_xx vpps. I know they're used in SP, but not sure about MP. No tools to edit the contents of these either.
    voices_en_us.vpp_pc
    voices_es_es.vpp_pc
    voices_fr_fr.vpp_pc
    voices_it_it.vpp_pc
    voices_ru_ru.vpp_pc


*/
