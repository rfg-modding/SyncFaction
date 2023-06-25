using System.Collections.Immutable;

namespace SyncFaction.Core.Data;

public static class Hashes
{
    public static ImmutableSortedDictionary<string, string> Get(bool isGog) => isGog
        ? AllGog.Value
        : AllSteam.Value;

    // TODO: different VPP_PC files between steam and gog are listed in separate dictionaries
    private static readonly Lazy<ImmutableSortedDictionary<string, string>> AllSteam = new(() => Vpp.Concat(Videos).Concat(Common).Concat(Steam).OrderBy(x => x.Key).ToImmutableSortedDictionary());
    private static readonly Lazy<ImmutableSortedDictionary<string, string>> AllGog = new(() => Vpp.Concat(Videos).Concat(Common).Concat(Gog).OrderBy(x => x.Key).ToImmutableSortedDictionary());

    public static readonly ImmutableDictionary<string, string> Vpp = new Dictionary<string, string>
    {
        { "data/activities.vpp_pc", "99eb018c1b0032d30788f80a49e3998caf03cb937dd3c9a67dd0f6b2816faf0f" },
        { "data/anims.vpp_pc", "97ed7f3937f8f15e1a7781a869133509ab4ee8b65fd3e6a822aad843142beaa5" },
        { "data/cloth_sim.vpp_pc", "594a82bc655ab2ccc94e1f6d07e92dc3d1b7dc8f515cab635d5b469d1abb168f" },
        { "data/decals.vpp_pc", "671f3c3645fb6ecf807d8ee427a2193797b4a898c0d904744bd3c9e250301198" },
        { "data/dlc01_l0.vpp_pc", "56f3daf37d66bbbdf3675189a71b0568bb48812ec42b994283cc5b3c899012db" },
        { "data/dlc01_l1.vpp_pc", "c658f235d6c6528aeaede8e8603f5d8bbac98b9980e0a935174aebddd443c12c" },
        { "data/dlc01_precache.vpp_pc", "ffa9b834735ceb05839725e4ebe0e378622bed60b69b7ea37d56474d80865cdd" },
        { "data/dlcp01_activities.vpp_pc", "6d3c7a905e7e9e5c8d9bee07b9ec85473cef1e5e43504fab7be4426a85208a7f" },
        { "data/dlcp01_anims.vpp_pc", "7e0e325f7531da3bfbb6a9491dd1d6f3b5637a36841f7e0029bda6a3133eb400" },
        { "data/dlcp01_cloth_sim.vpp_pc", "3073e78203f7a2efe62ee8f411648c7ac63a458df9fd110d4ec2e3e88ee11ea7" },
        { "data/dlcp01_effects.vpp_pc", "e3e6e097789d026015218210998619eed4be21fff217cad1f2338db4265b5ba3" },
        { "data/dlcp01_humans.vpp_pc", "851d6ba9d67baf8c6bceddb93cc0e1f96864181809955663221e9bb751d91411" },
        { "data/dlcp01_interface.vpp_pc", "bc3b9dec6db5a6984c792a10560409804c4ba6726828ed979e1b99f403c5ea2d" },
        { "data/dlcp01_items.vpp_pc", "02f23a9d35822adde386295741ff876959ac8cdf915a0b846e99eab0774cc91a" },
        { "data/dlcp01_misc.vpp_pc", "bbdd1bbbc68ef610883c47efaab949af41c1d1393dbef796c1fec50bf538265d" },
        { "data/dlcp01_missions.vpp_pc", "250a8d74e998e1a87e1da0c5dcb537b47237663d710ea7be4f5b41c4da2e9a56" },
        { "data/dlcp01_personas_en_us.vpp_pc", "b0d90718a849dfba1794cdb6d14aef2eb9d605c7452d2fbf680bbe95e4cd24f3" },
        { "data/dlcp01_vehicles_r.vpp_pc", "0512ceac75a4fcbe1050b87eefc8543c653f120c72564ba309309a4d9709d9e4" },
        { "data/dlcp01_voices_en_us.vpp_pc", "6fbfa8b6e85dd66e1e79358dc1fe630abd5e111069641116831389730558b56e" },
        { "data/dlcp02_interface.vpp_pc", "6a9efe4147f47f763af58c07cb79c7f3c3dadc3d2540f33c9ed313be2f9b2c72" },
        { "data/dlcp02_misc.vpp_pc", "26f0974b5baf8c6441293e2abbdccc983c467c4f2c2a963d496bf6d162fc05ab" },
        { "data/dlcp03_interface.vpp_pc", "21522b567262f03de5bdea9d446a5c8c583fb227d06173d4e514b58e114061fb" },
        { "data/dlcp03_misc.vpp_pc", "8a1259de265beb13e57ba4092c403deebca30a7e8e84e121db08d2b35c8f0ca8" },
        { "data/humans.vpp_pc", "d2434bc78aef11de6149a87dbfe377a33102e3f2b00bfa6654fe61facd6340cb" },
        { "data/interface.vpp_pc", "52b8ccc9acf7169a6958d8d4aafea989280b58785387184542350b8e07920040" },
        { "data/items.vpp_pc", "27c5e51437c2e27389e0cfbe43c9198ba17b547a3ce57d3f122067cae98abdc2" },
        { "data/items_mp.vpp_pc", "256c9ff01d34c27733af0d9f59158d3ae683e6eb50d2f2853ee204fa0b41d39a" },
        { "data/misc.vpp_pc", "9430a4e158ca914a381bd8474b56224163a4836683f4c9ebb18d6ab8cdcbfc10" },
        { "data/missions.vpp_pc", "a9082da7689b7b0c2261570571315df4006aafb7df70b96c1bbff149d4f1d70a" },
        { "data/mpdlc_broadside.vpp_pc", "38a04d2ad153e4094da3b34f5a6b7d83a0397e841d458fcda08173279fb82955" },
        { "data/mpdlc_division.vpp_pc", "c30f1bb009bf7ad7b0803a0b0302445aa89fa6534811306b7716d4b5e620e47b" },
        { "data/mpdlc_islands.vpp_pc", "dac2cd35ffb4f9f55de67be7173ade132329c9709eb186495f1cbde0f2ff0ed8" },
        { "data/mpdlc_landbridge.vpp_pc", "d0fd039379f90466424098b746c1b070f89204d9481fbd5a3380d36f18861b94" },
        { "data/mpdlc_minibase.vpp_pc", "52e4d5cde529b7bff50a78e226c89b5685a1cf3e4f41d5875d34b6370bff1854" },
        { "data/mpdlc_overhang.vpp_pc", "c471ea7cae9b2901cfe8771c52773eef8b2e39cecae9f93d4532963a5eb49a70" },
        { "data/mpdlc_puncture.vpp_pc", "63366c488eea981786833aea85a3d4279fbbae76298be0b411f7d14752108120" },
        { "data/mpdlc_ruins.vpp_pc", "ca84c091f781c2d1e7ec516b70436a0a44447f0dd56053ebfbb23184ad42b7c0" },
        { "data/mp_common.vpp_pc", "1458cc60012826d94b775c1542881d4209e679725769f9f042c3b57e94ac5d33" },
        { "data/mp_cornered.vpp_pc", "003c8447bef565f8bcfa631a776a9d8774897b3b3983ed3973ea48a4a50b4c96" },
        { "data/mp_crashsite.vpp_pc", "1c7c3dbd856b18a9cece555ee554f697fc4a41e6e8a13d5442f1cfab553ed6e2" },
        { "data/mp_crescent.vpp_pc", "fc0458286bd39ea5324e0efca8f05adddcf28d44311dd12aaf052d08be9282a4" },
        { "data/mp_crevice.vpp_pc", "0289b371bb4a4114dbae530ff5aeb445f9409342ea45f828e9ce837cd5c4c5ed" },
        { "data/mp_deadzone.vpp_pc", "d1248dda632d6c9ff35fb258c74f94afb6d4782fe6b27e7892f9f638558c98b1" },
        { "data/mp_downfall.vpp_pc", "754461f5f3046824fc5b6fe90d20d665eee403cb588b9d7688b3356d1ed0c6fe" },
        { "data/mp_excavation.vpp_pc", "a2d7bb01d1cc56107b37eadf9321965b8aa7b9de8031ca72fd86999aa2fff3e2" },
        { "data/mp_fallfactor.vpp_pc", "92b5a7bb73c1963ca7893cb57d90b8b5257f74d0c9ebc69a7e043c7bba0b5839" },
        { "data/mp_framework.vpp_pc", "2f7c8359c9b67002d407cba3500814ad0110a07cb3e4c486b8f5a609f9e68ec0" },
        { "data/mp_garrison.vpp_pc", "8ffd39525a2c2b3545b3ff00f6b419f40d1ed1cd9971552cbc05ea6dc0822dfc" },
        { "data/mp_gauntlet.vpp_pc", "0b8bc3b20b361e2c625e7daa34180ecfa357af5507c650391abf6fe9bc95df23" },
        { "data/mp_overpass.vpp_pc", "51476ec89f342c5adc02d52052a2e326f9246edc168208f7e6307fcb91d6df7c" },
        { "data/mp_pcx_assembly.vpp_pc", "68abe655399008127dbd545b3b5b18618d424bffe7bcbcc426e7328e0351564e" },
        { "data/mp_pcx_crossover.vpp_pc", "08c60ecc33e764cd4463d06bde30a139470be4f107ec50f45c55b20de58a6fa6" },
        { "data/mp_pinnacle.vpp_pc", "56a77914a86991dc993bacb07584804318e008a0a4f14c6508d15a25a1958cab" },
        { "data/mp_quarantine.vpp_pc", "7c3d5be4a5b9d545431e6c7fe8962333a41bcadcde0834b2f5a0af21afd45639" },
        { "data/mp_radial.vpp_pc", "023598e493c5f30e8175077529dff027093a9eddde2a2707911ee77e9f2041fb" },
        { "data/mp_rift.vpp_pc", "582cff5a9b2aedba9feb3a06459f519ee81c60c2688bceae8ffd5ac711b01291" },
        { "data/mp_sandpit.vpp_pc", "9f726ae42e5d9bad5aaf7235cc2c3c40ac7a395529cb2495d5c2997924c2ed91" },
        { "data/mp_settlement.vpp_pc", "c912a2da75ca9a0f625fabcaa3aebbe5ab24a014b4daf28404984cc8b8411243" },
        { "data/mp_warlords.vpp_pc", "6ec0bd0e3aa420f69e74a36e52f1f0860d7c024b886717cd1a1de9297dca4a2f" },
        { "data/mp_wasteland.vpp_pc", "1b7e17cdd309204183668dd7adae1cb0184acbc7c8381c3252c79c0b0f3a4973" },
        { "data/mp_wreckage.vpp_pc", "0ae900aa5ed46878a646ddcaa8bd260623171dcfe69cbe13906307f5bed736aa" },
        { "data/personas_de_de.vpp_pc", "5015dacdbac603f88798ab7f0d36d6099c30b2c10fad11e1bb7476c077cecb12" },
        { "data/personas_en_us.vpp_pc", "f207eb9b95988b4dfab08ed39970601a278bbc9e8b881710ba0c645df5391213" },
        { "data/personas_es_es.vpp_pc", "04e5b72a791aca0eb95f04b13ba57d780e2e567efa19d223c0dbe1a1e793da71" },
        { "data/personas_fr_fr.vpp_pc", "da4e47055d562e9ffe0273e187d40ea8d48797d97440c61bcabc7f88bf185faa" },
        { "data/personas_it_it.vpp_pc", "b6ff4eb29ecce5709656b04bba72fca03956b0010811c453f35c36e7e05892e5" },
        { "data/personas_ru_ru.vpp_pc", "27a7f4d1e7c3f516d54e414542a315ff3763c2e2f8aeaa52295162a378108018" },
        { "data/skybox.vpp_pc", "b02be5b2f10224ea6e8ba730ff0aa9ac58f8153b5606fb074dd35a90381e38da" },
        { "data/sounds_r.vpp_pc", "4be97eeaa4667557af16aee9bfc0da3e1c2dbf5375ba1c8d5151fba98b9ee43c" },
        { "data/steam.vpp_pc", "f41cd9e4c321173a97e7249c57cd84abad54993b37a0c9d9aca8ddb4bca85b4f" },
        { "data/table.vpp_pc", "8bd7205b68ce105e9ad04f495c5570d9e2dc51fec67bc9c1f0889768c2f9aef9" },
        { "data/terr01_l0.vpp_pc", "7f2d874589b119dbd1f3c0bbc15ea13873b6f379ee413d3a51dd78dd2dbd2ad0" },
        { "data/voices_de_de.vpp_pc", "829faae46ec48d94830e81bd8c3aa77755fbcb9117cb6fb8c7b473bb01e473af" },
        { "data/voices_en_us.vpp_pc", "47243ead34074ba575fdf20c50af3528911e21a2fc9b0deec2f098a313c775f5" },
        { "data/voices_es_es.vpp_pc", "6e50e28b65641fbc6dcc028b9c1afc820fd77596a8b4ebe6203076e7f35901a4" },
        { "data/voices_fr_fr.vpp_pc", "18588b88fa18fb51f7ac77a2377d902b383986d12d70dddd835c919f72f6a116" },
        { "data/voices_it_it.vpp_pc", "f085a5506a3208108eca1ad0cfb8794e6e42f0292eed0c7e7042e21f2d6fbb13" },
        { "data/voices_ru_ru.vpp_pc", "b8a28157d90980485a4b2be8d4ff3c72f4fb697e93f14a41fd9318efa210538e" },
        { "data/wc1.vpp_pc", "2b82a9e16c5f0fb6b7ce1f43f28bff0fa4a5351be8ee3d253a4c75c0785c73f6" },
        { "data/wc10.vpp_pc", "cd3614d0ea3a87842363c25918377162758b887eb5341b88b503a84d67ad6153" },
        { "data/wc2.vpp_pc", "26a2ac2aeb9da1fcf762becfd34f76a6e36c4aaefb978ec9a5475998ec9f34fa" },
        { "data/wc3.vpp_pc", "9bdb64a8189d7d0091521ecb7b491ca5c14ab61c10a905e509168ff418e7b613" },
        { "data/wc4.vpp_pc", "5ad4b021ed96fee689b177ab24b6291fd5381a8b2d4b8000eaf2ebab41472b65" },
        { "data/wc5.vpp_pc", "00eb38007759582f4e645d27e2af3adabf244772b182abcf92f84086864c8934" },
        { "data/wc6.vpp_pc", "ae75ba3039b1ddfebf8ccfc02ea5ceecf4b450553b9f08b8625756907ddf7fee" },
        { "data/wc7.vpp_pc", "1a7df1d41fd3d9fe88a4202706c21468b2a1921a971162633b7e3e773e0fb23b" },
        { "data/wc8.vpp_pc", "9d9fcbb06db0bf3e5ae250ca18970de526b7868089703ab44d46291d35ed74b6" },
        { "data/wc9.vpp_pc", "4117cfc1dc15ab57422a63aa9f458685006e6ab286c83bc538b10cfc77147a4b" },
        { "data/wcdlc1.vpp_pc", "ecf90efbef1cabc91ea7b96d2d82607ba706f1d55fd8262594b0ab2893bf5ac4" },
        { "data/wcdlc2.vpp_pc", "6d87a31e9d36cf70505a948f215849449ffa29d7fe9b2a88cb52d66e248a0d30" },
        { "data/wcdlc3.vpp_pc", "088eded2f6655b92dd1b7d87a01232589772c05ffae8697b2d5229c8818c6e78" },
        { "data/wcdlc4.vpp_pc", "f268dd39a429db86e58bbe8a2a4a50f5103551de7141ffd5670d7d249eda4c6d" },
        { "data/wcdlc5.vpp_pc", "df5e5109d7a703b6ac55aaf40110eabd089cde832ac9c4429e73f8682cfb9665" },
        { "data/wcdlc6.vpp_pc", "569b8356caf59fcb878052e08d3414293f89bda422bd12c4f33530fe6f4d1062" },
        { "data/wcdlc7.vpp_pc", "a8f7996d5fb7cace81bd3229c275e07f99c4ecc31ef11e52890058ca9b2d4a17" },
        { "data/wcdlc8.vpp_pc", "aa83f2e83f4630aef4395085fa5b241f19195ffdfea7b14c955764f09cdb4c56" },
        { "data/wcdlc9.vpp_pc", "0995c75937603864a63c1544b2796123c3aa23f6666779e6bbcfabdda6b0166c" },
        { "data/zonescript_dlc01.vpp_pc", "ed70213870eb81e0fc12c658b3934642e78e407560f143536eb46db98f0c0d2b" },
        { "data/zonescript_terr01.vpp_pc", "04b9db2005d1d621d4acda7ba7438fde39864c7986d90c063acd8f2487343c79" }
    }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, string> Steam = new Dictionary<string, string>
    {
        //{"rfg.exe", "0d52039e7f2d3f25a4be52a2aba83919456fb3f00e52e75051726247471a2df4"},
        { "steam_api.dll", "d99d425793f588fbc15f91c7765977cdd642b477be01dac41c0388ab6a5d492d" },
        //{"sw_api.dll", "6f813445ff757f70164c0103833478240e339d5e81dcbc5c4be238264380c89d"},
        //{"thqnocfg_steam.dat", "8357f2c8b9c3be4bcaf780271a1f3b76f9f4cad8dbf1b410fb4cd70cc4851186"},
        { "data/terr01_precache.vpp_pc", "2003c162f00e8cd7e2d58be99832532bc7bd9c9271329f08e37a40db94f4e4f1" },
        { "data/vehicles_r.vpp_pc", "9917023b74f3da29089bff3623f23b94e75e58edb0bdc9a007ee2ff5ded53383" },
        { "data/terr01_l1.vpp_pc", "92dc6908affec3806a1cf8d0624ad9aad21f95d97e3ceb39d19ac0f97b56f959" },
        { "data/effects.vpp_pc", "7c357a643e33b4e51cc4ad134f5149a88b3851cd97a7b9728d2700aaa26d8885" },
        { "data/effects_mp.vpp_pc", "6546b9e83043ffedb01867d3a3b7e37c37133ccb2337688bd0420ba88837f0fc" },
        { "data/chunks.vpp_pc", "9c748006b1304bae4a834d3a4360bd25c59a34d0ba0827ad5588df20eaadfa51" }
        //{"rfg.pdb", "8ecbdf5c89bc705b97b4184932da45817f06b512507acebe1083342c25b1f6df"},
    }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, string> Gog = new Dictionary<string, string>
    {
        { "Galaxy.dll", "bafeb03ca094e95226b4992314b15118c54f582da3c4b0401c59c92c3f472191" },
        //{"GalaxyPeer.dll", "9bfd8835020ef832001c7893df070af1f110d5beebf86e87b6133665c5329590"},
        //{"rfg.exe", "7a82d2d0f425af5e75d8ffbce12fac53eb5ca9cd812731ccf5a29697e906af0e"},
        //{"sw_api.dll", "3d5b41308c20dc9df779f8e35ceaa303fab0de8d6304b6d7346105fd95d8a24f"},
        //{"thqnocfg_gog.dat", "6f0427b331306c823afdc21347366c822645a6eea4c64d241bbe9e29de7e0c1d"},
        { "data/terr01_precache.vpp_pc", "fee2544b55f0fd6f642a23bbaeadbefd9d0fb76b05656f591a2e292c6a759590" },
        { "data/vehicles_r.vpp_pc", "8411b3c42249458cd51741a348ed1b8713b2ec4652d4efffe199bc88e2d4753f" },
        { "data/terr01_l1.vpp_pc", "a89bfd5405f42bdb13b7447653d6d70d1fff1a753f6d4def7f390f2af0a28b47" },
        { "data/effects.vpp_pc", "6277e9df4d471f32c1bf7a7a18265eaae41423366c43f9e7b3d07d33190b4eba" },
        { "data/effects_mp.vpp_pc", "ce8e3b09f1eae980f0ccba5536d8af148300899ca3091b4fefb8981256c86937" },
        { "data/chunks.vpp_pc", "5c2c1d7a62e9bd1d52815918b0acb67ecdf7efc05afcd46f3b91f4bf671a1973" }
        //{"gog.ico", "f6a71321521ba89713f0bd38b21f809e87e8a789cb172e8e4693f9479e30b1e4"},
        //{"goggame-2029222893.hashdb", "15fa43afe01eabd14e282c6bca512006a1c2003a4c493556315820980cbd2049"},
        //{"goggame-2029222893.ico", "405ef739021f1b807a8e4085331aabeb3070c65e9de0aaabd8f69643262cb51b"},
        //{"goggame-2029222893.info", "e4054017952615bb6e104a91136dad4962a412548ec2424e7bc22b09b9fb0f8e"},
        //{"goggame-galaxyFileList.ini", "100bc8cdda0fc9e6c5234f09eda38a8f724f649c1ee3b989c0705be6d6e113de"},
        //{"rfg.pdb", "aebb037679f7420ebb105739dee5dec796ac337f5bf3e16a05c4131b302c4ab2"},
        //{"support.ico", "05fe749ef47d1ec862d6c55be78e66d1011226bf1f78409acf57cb79cec5eb20"},
        //{"unins000.dat", "ddab49ab4ed1810012622a506b4e6e2ecafbe92f651eb4f06ea271876732b933"},
        //{"unins000.exe", "5761e7789d813626cd68ee1e62429cfeb92bdd814cd29ef12fc4ae9ec1dbaff3"},
        //{"unins000.ini", "5517cc7f1579c2735ec4a88177e0b180a8e323f96ec1bbbe77d9ec5a75f09d42"},
        //{"unins000.msg", "e43d06ec2b3dbe3d81bcd6b7880d28d074dac54b38646a605cfc5c809939da16"},
    }.ToImmutableDictionary();

    public static readonly ImmutableHashSet<string> MultiplayerFiles = new List<string>
    {
        "misc.vpp_pc",
        "table.vpp_pc",
        "anims.vpp_pc",
        "decals.vpp_pc",
        "skybox.vpp_pc",
        "sounds_r.vpp_pc",
        "steam.vpp_pc",
        "effects_mp.vpp_pc",
        "items_mp.vpp_pc",
        "mpdlc_broadside.vpp_pc",
        "mpdlc_division.vpp_pc",
        "mpdlc_islands.vpp_pc",
        "mpdlc_landbridge.vpp_pc",
        "mpdlc_minibase.vpp_pc",
        "mpdlc_overhang.vpp_pc",
        "mpdlc_puncture.vpp_pc",
        "mpdlc_ruins.vpp_pc",
        "mp_common.vpp_pc",
        "mp_cornered.vpp_pc",
        "mp_crashsite.vpp_pc",
        "mp_crescent.original.vpp_pc",
        "mp_crescent.vpp_pc",
        "mp_crevice.vpp_pc",
        "mp_damage_control.bik",
        "mp_deadzone.vpp_pc",
        "mp_defensive_packs.bik",
        "mp_demolition_mode.bik",
        "mp_destructive_packs.bik",
        "mp_downfall.vpp_pc",
        "mp_excavation.vpp_pc",
        "mp_fallfactor.vpp_pc",
        "mp_framework.vpp_pc",
        "mp_garrison.vpp_pc",
        "mp_gauntlet.vpp_pc",
        "mp_movement_packs.bik",
        "mp_overpass.vpp_pc",
        "mp_pcx_assembly.vpp_pc",
        "mp_pcx_crossover.vpp_pc",
        "mp_pinnacle.vpp_pc",
        "mp_quarantine.vpp_pc",
        "mp_radial.vpp_pc",
        "mp_recon_packs.bik",
        "mp_rift.vpp_pc",
        "mp_sandpit.vpp_pc",
        "mp_settlement.vpp_pc",
        "mp_siege_mode.bik",
        "mp_support_packs.bik",
        "mp_warlords.vpp_pc",
        "mp_wasteland.vpp_pc",
        "mp_wreckage.vpp_pc",
        "wc1.vpp_pc",
        "wc10.vpp_pc",
        "wc2.vpp_pc",
        "wc3.vpp_pc",
        "wc4.vpp_pc",
        "wc5.vpp_pc",
        "wc6.vpp_pc",
        "wc7.vpp_pc",
        "wc8.vpp_pc",
        "wc9.vpp_pc",
        "wcdlc1.vpp_pc",
        "wcdlc2.vpp_pc",
        "wcdlc3.vpp_pc",
        "wcdlc4.vpp_pc",
        "wcdlc5.vpp_pc",
        "wcdlc6.vpp_pc",
        "wcdlc7.vpp_pc",
        "wcdlc8.vpp_pc",
        "wcdlc9.vpp_pc"
    }.ToImmutableHashSet();

    private static readonly ImmutableDictionary<string, string> Videos = new Dictionary<string, string>
    {
        { "data/ants_vs_magnifying_glass.bik", "b9ddd28cff61a7b528570d7ed7cd2552b69432664abf7aa66ed1fe4711ae51b9" },
        { "data/assault_the_edf_central_command.bik", "42d55c3d219eb4eed75b8b132225d306a7f2400dd7acdfbcd0e15765332cc347" },
        { "data/control.bik", "07bb32ee34f2668d3b9630a96ea0aab757f901705c4856cbfbaf9fa3fda7108a" },
        { "data/death_by_committee.bik", "a44156b62c3c183cfff235585ea712c1a11a6fe600248e796c10951b9cb755f6" },
        { "data/death_from_above.bik", "73fc3fbd1ece4aa10e785b175154283a6b1a40ad87508f366d480b624300e2d6" },
        { "data/dlc_cine01.bik_xbox2", "e24a6d5a96d935cb624294c5dc9809e9f2312bbc02d8ff1a4b1991a955691a09" },
        { "data/dlc_cine02.bik_xbox2", "32f89b15f09e1a7da2202259fe02d4d8945a142184cbffd6b6330b944fc22e46" },
        { "data/dlc_mission_1.bik", "0c86324600191faa57bb2dd847f54727dadbc0efa2c35633db860ad0bc57152c" },
        { "data/dlc_mission_2.bik", "fdd48abf0331b90710f59fdc019e0c8e6adea00903083bc61d61fa72ddbdc015" },
        { "data/dlc_mission_3.bik", "028f227ce7547e894cbe6e97711a99e0e4b56fa5790af8ce955304243d868d39" },
        { "data/emergency_broadcast_system.bik", "e657cc2e8fa6aa5b9701778ebd63eb7b36732beaa81105164b74819f87d3b175" },
        { "data/final_mission.bik", "a023880d348fd1285afd26c89be7f7e8037325ed40885881f62d13340fe1e81d" },
        { "data/friends_martians_countrymen.bik", "f4debdc58f83856a42914c46a8066ae0155d5446b732ce0dfffbcddf43d37f25" },
        { "data/guerrilla_war.bik", "ca70f5d0dc3c766b3d48325518ccb06a5562ca09cf681f9f48186e15b8e12047" },
        { "data/guns_of_tharsis.bik", "4fd0184f4019b93931f85d4e81fac207b5912c299ea8907d541e030dbcfdaa1b" },
        { "data/highway_to_hell.bik", "8c6b72877a59192475bbf9fcbd0cbf9bb62ddbd8d9192dab108b5a6793cc3e0f" },
        { "data/intro_1.bik", "a74d7d3f1228516979b860ee9db15bb0adac07562da70b1d3fd13ca7815feef3" },
        { "data/intro_2.bik", "455826237b7cd101dbbd721cb8c30fa13cfc2d953fca47d9d07e3ca12351f03b" },
        { "data/legal_hd.bik", "5aba6b4db40219cab866f8007ea3f2f9d22baf104d80cef0e7572c90d6515ed6" },
        { "data/logo_kaiko.bik", "35414dd451a5c6aab8f48c45bb26861c3624d43c7d884de4b9448be8e6dffd32" },
        { "data/logo_nordic_hd.bik", "189f162a09042862403942e553d78669acf8730ea1b27c3b88585124f9b75528" },
        { "data/logo_volition_hd.bik", "f8581cef86ed2c8ffda0db03d2c7ec3ed815601d6baef2ce0c07dbcf7ff58f02" },
        { "data/mb_placeholder.bik", "1ddcbc82f607be4fd785b1debf9646fbc91a05d4974f64155a4b0937135533ba" },
        { "data/morale.bik", "18f92f8e0cc79faf3d71d608bb1703df26fd9607f172f259fe1d7c5555ed8d34" },
        { "data/mp_damage_control.bik", "679f2a5393469f1165ccfbbdcc1b1d492079eb59ad58e53796f6361e5601b265" },
        { "data/mp_defensive_packs.bik", "b21ea5e83004327abdadda0e155e9470890232f294c0b5f3d0af933945faec8b" },
        { "data/mp_demolition_mode.bik", "962ec294f43cd7bf41ffbcf19ba31f881c6c7832df2fe88ebba3eb0d9c6fae3e" },
        { "data/mp_destructive_packs.bik", "b87b2c58d73c6830a7256995fc1ef3b1cb0bf1b6b2ab3ddda0f3feda00b74c18" },
        { "data/mp_movement_packs.bik", "cfd9e9815f17018bb349dd0b77cc492b8d29a08fdce80c71f8adcde8809dabb1" },
        { "data/mp_recon_packs.bik", "3ef0cc5ed5aa8790cac47fc68078e102f08432a7bf0ddaf27b1ad251a97ce7f4" },
        { "data/mp_siege_mode.bik", "9e8cf571565da4bc2860c9e1548386d565766745d37cffd03528415f25089a9b" },
        { "data/mp_support_packs.bik", "46b08139e4de9e65efdbb8a007e167f5297bd5fddcbe6c655a9fbfb9a7540f54" },
        { "data/parker_liberation.bik", "d0b01d26c446e191ddf33f0f4c5d4e7d8bb5584e2001530cd8ae0a64bb7d086a" },
        { "data/partytime.bik", "bfd02c49589d1a2a444db75d28e567fe2e11df8b04687e5e9bdab421ac509feb" },
        { "data/refugee_truck.bik", "e3d4c65f1b4eb85e1e7f1fe89998b972b4da741d635effbe22a88efb02e14cee" },
        { "data/rfg_cine_00a.bik_xbox2", "a520422d0bb88d24adc32893ad33ea079a686051c623c57b134aec44e4581c7e" },
        { "data/rfg_cine_01a.bik_xbox2", "796a7e6343c77b7f39e930682c3f66a8f605de592cbac57b306af4eedd884e27" },
        { "data/rfg_cine_02a.bik_xbox2", "2d2ae4278682dd0d74f594832a7c7f57c037d60de2602d4ab1c6502d6018c30c" },
        { "data/rfg_cine_03a.bik_xbox2", "78360357c83abb4484642e21183932119cd38d51a8c164f34d60ad44755f2d81" },
        { "data/rfg_cine_03b.bik_xbox2", "f5d26e61da908a20fc912be4e4c055159fdd6c31ea0306749ba30ff7b63743fc" },
        { "data/rfg_cine_04a.bik_xbox2", "ce920a0168bb947458ef78d59e8c9d94d38c6bd86cabccc73ef37ee73f8bd214" },
        { "data/rfg_cine_05a.bik_xbox2", "99956bd7e4568d6dbca544d8d4e6bde45e0bc830f45e9b4ec3dbf3e0a9f00654" },
        { "data/rfg_cine_06a.bik_xbox2", "ecbc5da9a9c4ea3176d1c94c3d5d44c46e7797724f09644a29778edb72721a9a" },
        { "data/rfg_mainmenubg.bik", "3fda3d6bfbba15b97d4cc318073641bc4363255f17fef562122bc02181eefbb3" },
        { "data/salvage_and_upgrades.bik", "43154455cf19d7027c5104cdab6144cae66a3ba480d9fb571610d441b53498c5" },
        { "data/save_the_guerrilla_camp.bik", "08bc642c179153906088d1bb5067d8607c00619af85663ee88aabde6f473563c" },
        { "data/sniper_hunter.bik", "c437444d2a58db0052fbd0b5c98fd411b9538da07a89b8cb07a511e4af916014" },
        { "data/start_your_engines.bik", "a2eb29f6baa6e85b382622d3998a33769ee5b83ad572523ad1a677e33407925d" },
        { "data/tank_attack.bik", "99e131c61f521a2a7a720cb554fb8ee9c9491a12e09855c0984034b6ae929904" },
        { "data/traffic_jam.bik", "5994b7e8f269f3755d64256d7e92f18dab5f4b758f04095ff74c2048a0ddd4a7" },
        { "data/walker_martian_ranger.bik", "b5ae538bc465285526d4254701e64b88ac9b81ccbfc2be33c7d5406a6a82632b" },
        { "data/we_know_where_you_are.bik", "1efaa2d4ec0fd4495ccade09de8928853f7c18365dd406c4250163fe4ba52788" }
    }.ToImmutableDictionary();

    private static readonly ImmutableDictionary<string, string> Common = new Dictionary<string, string>
    {
        //{"GfeSDK.dll", "fffaf72a3d5adbaa7df849afdb5af8c10fe3a42800786096f41dde1cc5847e14"},
        //{"binkw32.dll", "36810b3318acd07a5b4e78fe72db163ed63880ab2eb2d9b5bda0331ded32a9c3"},
        //{"discord-rpc.dll", "3ade46bd17d6cd114b0e7f79f8a1e985b9221b92d897c28d32d659f032991bb1"},
        //{"dualshock4.padcfg", "fb551911de7edcd98b22dbfc4d6b6e5bf3327e560b03e51f74197a27cf6b3175"},
        //{"libeay32.dll", "3dc13114435872a72a26b16e67c3cda764d44aa20a44105835e28a4ea6021679"},
        //{"ssleay32.dll", "e05800580e4f8bba4c965734d1231f0b80897f826326c52bbdce41d81c452c3c"},
        //{"switchprocon.padcfg", "794cc295d29cb4667b77f84a371e5ee3a612806095f1a3a457e0de21cc924fcd"},
    }.ToImmutableDictionary();
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
