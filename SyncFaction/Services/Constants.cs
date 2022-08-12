using System.Collections.Generic;
using System.Collections.Immutable;

namespace SyncFaction.Services;

public static class Constants
{
    public static readonly ImmutableDictionary<string, string> KnownFiles = new Dictionary<string, string>()
    {
        { "activities", "99eb018c1b0032d30788f80a49e3998caf03cb937dd3c9a67dd0f6b2816faf0f" },
        { "anims", "97ed7f3937f8f15e1a7781a869133509ab4ee8b65fd3e6a822aad843142beaa5" },
        { "chunks", "9c748006b1304bae4a834d3a4360bd25c59a34d0ba0827ad5588df20eaadfa51" },
        { "cloth_sim", "594a82bc655ab2ccc94e1f6d07e92dc3d1b7dc8f515cab635d5b469d1abb168f" },
        { "decals", "671f3c3645fb6ecf807d8ee427a2193797b4a898c0d904744bd3c9e250301198" },
        { "dlc01_l0", "56f3daf37d66bbbdf3675189a71b0568bb48812ec42b994283cc5b3c899012db" },
        { "dlc01_l1", "c658f235d6c6528aeaede8e8603f5d8bbac98b9980e0a935174aebddd443c12c" },
        { "dlc01_precache", "ffa9b834735ceb05839725e4ebe0e378622bed60b69b7ea37d56474d80865cdd" },
        { "dlcp01_activities", "6d3c7a905e7e9e5c8d9bee07b9ec85473cef1e5e43504fab7be4426a85208a7f" },
        { "dlcp01_anims", "7e0e325f7531da3bfbb6a9491dd1d6f3b5637a36841f7e0029bda6a3133eb400" },
        { "dlcp01_cloth_sim", "3073e78203f7a2efe62ee8f411648c7ac63a458df9fd110d4ec2e3e88ee11ea7" },
        { "dlcp01_effects", "e3e6e097789d026015218210998619eed4be21fff217cad1f2338db4265b5ba3" },
        { "dlcp01_humans", "851d6ba9d67baf8c6bceddb93cc0e1f96864181809955663221e9bb751d91411" },
        { "dlcp01_interface", "bc3b9dec6db5a6984c792a10560409804c4ba6726828ed979e1b99f403c5ea2d" },
        { "dlcp01_items", "02f23a9d35822adde386295741ff876959ac8cdf915a0b846e99eab0774cc91a" },
        { "dlcp01_misc", "bbdd1bbbc68ef610883c47efaab949af41c1d1393dbef796c1fec50bf538265d" },
        { "dlcp01_missions", "250a8d74e998e1a87e1da0c5dcb537b47237663d710ea7be4f5b41c4da2e9a56" },
        { "dlcp01_personas_en_us", "b0d90718a849dfba1794cdb6d14aef2eb9d605c7452d2fbf680bbe95e4cd24f3" },
        { "dlcp01_vehicles_r", "0512ceac75a4fcbe1050b87eefc8543c653f120c72564ba309309a4d9709d9e4" },
        { "dlcp01_voices_en_us", "6fbfa8b6e85dd66e1e79358dc1fe630abd5e111069641116831389730558b56e" },
        { "dlcp02_interface", "6a9efe4147f47f763af58c07cb79c7f3c3dadc3d2540f33c9ed313be2f9b2c72" },
        { "dlcp02_misc", "26f0974b5baf8c6441293e2abbdccc983c467c4f2c2a963d496bf6d162fc05ab" },
        { "dlcp03_interface", "21522b567262f03de5bdea9d446a5c8c583fb227d06173d4e514b58e114061fb" },
        { "dlcp03_misc", "8a1259de265beb13e57ba4092c403deebca30a7e8e84e121db08d2b35c8f0ca8" },
        { "effects", "7c357a643e33b4e51cc4ad134f5149a88b3851cd97a7b9728d2700aaa26d8885" },
        { "effects_mp", "6546b9e83043ffedb01867d3a3b7e37c37133ccb2337688bd0420ba88837f0fc" },
        { "humans", "d2434bc78aef11de6149a87dbfe377a33102e3f2b00bfa6654fe61facd6340cb" },
        { "interface", "52b8ccc9acf7169a6958d8d4aafea989280b58785387184542350b8e07920040" },
        { "items", "27c5e51437c2e27389e0cfbe43c9198ba17b547a3ce57d3f122067cae98abdc2" },
        { "items_mp", "256c9ff01d34c27733af0d9f59158d3ae683e6eb50d2f2853ee204fa0b41d39a" },
        { "misc", "9430a4e158ca914a381bd8474b56224163a4836683f4c9ebb18d6ab8cdcbfc10" },
        { "missions", "a9082da7689b7b0c2261570571315df4006aafb7df70b96c1bbff149d4f1d70a" },
        { "mpdlc_broadside", "38a04d2ad153e4094da3b34f5a6b7d83a0397e841d458fcda08173279fb82955" },
        { "mpdlc_division", "c30f1bb009bf7ad7b0803a0b0302445aa89fa6534811306b7716d4b5e620e47b" },
        { "mpdlc_islands", "dac2cd35ffb4f9f55de67be7173ade132329c9709eb186495f1cbde0f2ff0ed8" },
        { "mpdlc_landbridge", "d0fd039379f90466424098b746c1b070f89204d9481fbd5a3380d36f18861b94" },
        { "mpdlc_minibase", "52e4d5cde529b7bff50a78e226c89b5685a1cf3e4f41d5875d34b6370bff1854" },
        { "mpdlc_overhang", "c471ea7cae9b2901cfe8771c52773eef8b2e39cecae9f93d4532963a5eb49a70" },
        { "mpdlc_puncture", "63366c488eea981786833aea85a3d4279fbbae76298be0b411f7d14752108120" },
        { "mpdlc_ruins", "ca84c091f781c2d1e7ec516b70436a0a44447f0dd56053ebfbb23184ad42b7c0" },
        { "mp_common", "1458cc60012826d94b775c1542881d4209e679725769f9f042c3b57e94ac5d33" },
        { "mp_cornered", "003c8447bef565f8bcfa631a776a9d8774897b3b3983ed3973ea48a4a50b4c96" },
        { "mp_crashsite", "1c7c3dbd856b18a9cece555ee554f697fc4a41e6e8a13d5442f1cfab553ed6e2" },
        { "mp_crescent", "fc0458286bd39ea5324e0efca8f05adddcf28d44311dd12aaf052d08be9282a4" },
        { "mp_crevice", "0289b371bb4a4114dbae530ff5aeb445f9409342ea45f828e9ce837cd5c4c5ed" },
        { "mp_deadzone", "d1248dda632d6c9ff35fb258c74f94afb6d4782fe6b27e7892f9f638558c98b1" },
        { "mp_downfall", "754461f5f3046824fc5b6fe90d20d665eee403cb588b9d7688b3356d1ed0c6fe" },
        { "mp_excavation", "a2d7bb01d1cc56107b37eadf9321965b8aa7b9de8031ca72fd86999aa2fff3e2" },
        { "mp_fallfactor", "92b5a7bb73c1963ca7893cb57d90b8b5257f74d0c9ebc69a7e043c7bba0b5839" },
        { "mp_framework", "2f7c8359c9b67002d407cba3500814ad0110a07cb3e4c486b8f5a609f9e68ec0" },
        { "mp_garrison", "8ffd39525a2c2b3545b3ff00f6b419f40d1ed1cd9971552cbc05ea6dc0822dfc" },
        { "mp_gauntlet", "0b8bc3b20b361e2c625e7daa34180ecfa357af5507c650391abf6fe9bc95df23" },
        { "mp_overpass", "51476ec89f342c5adc02d52052a2e326f9246edc168208f7e6307fcb91d6df7c" },
        { "mp_pcx_assembly", "68abe655399008127dbd545b3b5b18618d424bffe7bcbcc426e7328e0351564e" },
        { "mp_pcx_crossover", "08c60ecc33e764cd4463d06bde30a139470be4f107ec50f45c55b20de58a6fa6" },
        { "mp_pinnacle", "56a77914a86991dc993bacb07584804318e008a0a4f14c6508d15a25a1958cab" },
        { "mp_quarantine", "7c3d5be4a5b9d545431e6c7fe8962333a41bcadcde0834b2f5a0af21afd45639" },
        { "mp_radial", "023598e493c5f30e8175077529dff027093a9eddde2a2707911ee77e9f2041fb" },
        { "mp_rift", "582cff5a9b2aedba9feb3a06459f519ee81c60c2688bceae8ffd5ac711b01291" },
        { "mp_sandpit", "9f726ae42e5d9bad5aaf7235cc2c3c40ac7a395529cb2495d5c2997924c2ed91" },
        { "mp_settlement", "c912a2da75ca9a0f625fabcaa3aebbe5ab24a014b4daf28404984cc8b8411243" },
        { "mp_warlords", "6ec0bd0e3aa420f69e74a36e52f1f0860d7c024b886717cd1a1de9297dca4a2f" },
        { "mp_wasteland", "1b7e17cdd309204183668dd7adae1cb0184acbc7c8381c3252c79c0b0f3a4973" },
        { "mp_wreckage", "0ae900aa5ed46878a646ddcaa8bd260623171dcfe69cbe13906307f5bed736aa" },
        { "personas_de_de", "5015dacdbac603f88798ab7f0d36d6099c30b2c10fad11e1bb7476c077cecb12" },
        { "personas_en_us", "f207eb9b95988b4dfab08ed39970601a278bbc9e8b881710ba0c645df5391213" },
        { "personas_es_es", "04e5b72a791aca0eb95f04b13ba57d780e2e567efa19d223c0dbe1a1e793da71" },
        { "personas_fr_fr", "da4e47055d562e9ffe0273e187d40ea8d48797d97440c61bcabc7f88bf185faa" },
        { "personas_it_it", "b6ff4eb29ecce5709656b04bba72fca03956b0010811c453f35c36e7e05892e5" },
        { "personas_ru_ru", "27a7f4d1e7c3f516d54e414542a315ff3763c2e2f8aeaa52295162a378108018" },
        { "skybox", "b02be5b2f10224ea6e8ba730ff0aa9ac58f8153b5606fb074dd35a90381e38da" },
        { "sounds_r", "4be97eeaa4667557af16aee9bfc0da3e1c2dbf5375ba1c8d5151fba98b9ee43c" },
        { "steam", "f41cd9e4c321173a97e7249c57cd84abad54993b37a0c9d9aca8ddb4bca85b4f" },
        { "table", "8bd7205b68ce105e9ad04f495c5570d9e2dc51fec67bc9c1f0889768c2f9aef9" },
        { "terr01_l0", "7f2d874589b119dbd1f3c0bbc15ea13873b6f379ee413d3a51dd78dd2dbd2ad0" },
        { "terr01_l1", "92dc6908affec3806a1cf8d0624ad9aad21f95d97e3ceb39d19ac0f97b56f959" },
        { "terr01_precache", "2003c162f00e8cd7e2d58be99832532bc7bd9c9271329f08e37a40db94f4e4f1" },
        { "vehicles_r", "9917023b74f3da29089bff3623f23b94e75e58edb0bdc9a007ee2ff5ded53383" },
        { "voices_de_de", "829faae46ec48d94830e81bd8c3aa77755fbcb9117cb6fb8c7b473bb01e473af" },
        { "voices_en_us", "47243ead34074ba575fdf20c50af3528911e21a2fc9b0deec2f098a313c775f5" },
        { "voices_es_es", "6e50e28b65641fbc6dcc028b9c1afc820fd77596a8b4ebe6203076e7f35901a4" },
        { "voices_fr_fr", "18588b88fa18fb51f7ac77a2377d902b383986d12d70dddd835c919f72f6a116" },
        { "voices_it_it", "f085a5506a3208108eca1ad0cfb8794e6e42f0292eed0c7e7042e21f2d6fbb13" },
        { "voices_ru_ru", "b8a28157d90980485a4b2be8d4ff3c72f4fb697e93f14a41fd9318efa210538e" },
        { "wc1", "2b82a9e16c5f0fb6b7ce1f43f28bff0fa4a5351be8ee3d253a4c75c0785c73f6" },
        { "wc10", "cd3614d0ea3a87842363c25918377162758b887eb5341b88b503a84d67ad6153" },
        { "wc2", "26a2ac2aeb9da1fcf762becfd34f76a6e36c4aaefb978ec9a5475998ec9f34fa" },
        { "wc3", "9bdb64a8189d7d0091521ecb7b491ca5c14ab61c10a905e509168ff418e7b613" },
        { "wc4", "5ad4b021ed96fee689b177ab24b6291fd5381a8b2d4b8000eaf2ebab41472b65" },
        { "wc5", "00eb38007759582f4e645d27e2af3adabf244772b182abcf92f84086864c8934" },
        { "wc6", "ae75ba3039b1ddfebf8ccfc02ea5ceecf4b450553b9f08b8625756907ddf7fee" },
        { "wc7", "1a7df1d41fd3d9fe88a4202706c21468b2a1921a971162633b7e3e773e0fb23b" },
        { "wc8", "9d9fcbb06db0bf3e5ae250ca18970de526b7868089703ab44d46291d35ed74b6" },
        { "wc9", "4117cfc1dc15ab57422a63aa9f458685006e6ab286c83bc538b10cfc77147a4b" },
        { "wcdlc1", "ecf90efbef1cabc91ea7b96d2d82607ba706f1d55fd8262594b0ab2893bf5ac4" },
        { "wcdlc2", "6d87a31e9d36cf70505a948f215849449ffa29d7fe9b2a88cb52d66e248a0d30" },
        { "wcdlc3", "088eded2f6655b92dd1b7d87a01232589772c05ffae8697b2d5229c8818c6e78" },
        { "wcdlc4", "f268dd39a429db86e58bbe8a2a4a50f5103551de7141ffd5670d7d249eda4c6d" },
        { "wcdlc5", "df5e5109d7a703b6ac55aaf40110eabd089cde832ac9c4429e73f8682cfb9665" },
        { "wcdlc6", "569b8356caf59fcb878052e08d3414293f89bda422bd12c4f33530fe6f4d1062" },
        { "wcdlc7", "a8f7996d5fb7cace81bd3229c275e07f99c4ecc31ef11e52890058ca9b2d4a17" },
        { "wcdlc8", "aa83f2e83f4630aef4395085fa5b241f19195ffdfea7b14c955764f09cdb4c56" },
        { "wcdlc9", "0995c75937603864a63c1544b2796123c3aa23f6666779e6bbcfabdda6b0166c" },
        { "zonescript_dlc01", "ed70213870eb81e0fc12c658b3934642e78e407560f143536eb46db98f0c0d2b" },
        { "zonescript_terr01", "04b9db2005d1d621d4acda7ba7438fde39864c7986d90c063acd8f2487343c79" },

    }.ToImmutableDictionary();

    public const string bakDirName = "bak";
    public const string appDirName = ".syncfaction";
    public const string ApiUrl = @"https://autodl.factionfiles.com/rfg/v1/files-by-cat.php";
    public const string BrowserUrlTemplate = "https://www.factionfiles.com/ff.php?action=file&id={0}";
    public const string WikiPage = "https://www.redfactionwiki.com/wiki/RF:G_Game_Night_News";

    public static readonly string ErrorFormat = @"# Error!
**Operation failed**: {0}

##What now:
* Use Steam to check integrity of game files
* Check if game location is valid and URL is accessible
* See if new versions of SyncFaction are available: [github.com/Rast1234/SyncFaction](https://github.com/Rast1234/SyncFaction)
* Please report this error to developer. **Copy all the stuff below** to help fixing it!
* Take care of your sledgehammer and remain a good Martian

{1}

";

}
