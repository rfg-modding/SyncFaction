using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.ModManager;
using static SyncFactionTests.Fs;

namespace SyncFactionTests;

public class FileManagerTests
{
    private ILogger<FileManager> log = new NullLogger<FileManager>();
    private CancellationToken token = CancellationToken.None;
    private string gameDir = "/game";

    private readonly ModTools modTools = new(new NullLogger<ModTools>());

    [Test]
    public async Task ForgetUpdates_Works()
    {
        var fs = Init();
        var hashes = Hashes.Empty;
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var subdir = storage.PatchBak.CreateSubdirectory("test");
        var dummy = fs.FileInfo.New("/game/.keep");
        dummy.Create();
        var copy1 = dummy.CopyTo(fs.Path.Combine(storage.PatchBak.FullName, ".keep"));
        var copy2 = dummy.CopyTo(fs.Path.Combine(subdir.FullName, ".keep"));
        copy1.Exists.Should().BeTrue();
        copy2.Exists.Should().BeTrue();
        storage.PatchBak.EnumerateFiles().Should().HaveCount(1);
        subdir.EnumerateFiles().Should().HaveCount(1);

        manager.ForgetUpdates(storage);

        storage.PatchBak.Refresh();
        storage.PatchBak.Exists.Should().BeTrue();
        storage.PatchBak.EnumerateFiles().Should().BeEmpty();
        copy1.Refresh();
        copy2.Refresh();
        copy1.Exists.Should().BeFalse();
        copy2.Exists.Should().BeFalse();
    }

    [Test]
    public async Task ForgetUpdates_NoDirectory_Fails()
    {
        var fs = Init();
        var hashes = Hashes.Empty;
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        storage.PatchBak.Exists.Should().BeTrue();
        storage.PatchBak.Delete();
        storage.PatchBak.Refresh();
        storage.PatchBak.Exists.Should().BeFalse();

        var action = () => manager.ForgetUpdates(storage);
        action.Should().Throw<DirectoryNotFoundException>();
    }

    [Test]
    public async Task InstallMod_EmptyMod_False()
    {
        var fs = Init();
        var expected = fs.Clone();
        var hashes = Hashes.Exe;
        fs.DirectoryInfo.New(ModRoot).Create();
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var mod = new Mod
        {
            Id = ModId
        };

        var result = await manager.InstallMod(storage, mod, false, token);
        result.Success.Should().BeFalse();
        fs.ShouldHaveSameFilesAs(expected);
    }

    [Test]
    public async Task InstallMod_NewFile_True()
    {
        var fs = Init(x =>
        {
            x.InitFile().Data("new file data").Name("new_file.bin").In(ModRoot);
            x.InitFile().Data("other file data").Name("other_file").In(ModRoot);
        });
        var expected = fs.Clone(x =>
        {
            x.InitFile().Name("new_file.bin").In(ManagedRoot)
                .Data("new file data").In(GameRoot);
            x.InitFile().Name("other_file").In(ManagedRoot)
                .Data("other file data").In(GameRoot);
        });
        var hashes = Hashes.Exe;
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var mod = new Mod
        {
            Id = ModId
        };

        var result = await manager.InstallMod(storage, mod, false, token);
        result.Success.Should().BeTrue();
        fs.ShouldHaveSameFilesAs(expected);
    }

    [Test]
    public async Task InstallMod_OnlyUnsupportedFiles_False()
    {
        var fs = Init(x =>
        {
            x.InitFile().Data("1").Name("unsupported1.jpg").In(ModRoot);
            x.InitFile().Data("2").Name("unsupported2.jPeG").In(ModData);
            x.InitFile().Data("3").Name("unsupported3.zip").In(ModEtc);
            x.InitFile().Data("4").Name("unsupported4.7Z").In(ModDataEtc);
            x.InitFile().Data("5").Name("unsupported5.Txt").In(ModRoot);
            x.InitFile().Data("6").Name("unsupported6.PNG").In(ModRoot);
            x.InitFile().Data("7").Name(".mod_unsupported7.vpp_pc").In(ModRoot);
            x.InitFile().Data("8").Name(".mod_unsupported8.exe").In(ModData);
        });
        var expected = fs.Clone(x =>
        {
        });
        var hashes = Hashes.Exe;
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var mod = new Mod
        {
            Id = ModId
        };

        var result = await manager.InstallMod(storage, mod, false, token);
        result.Success.Should().BeFalse();
        fs.ShouldHaveSameFilesAs(expected);
    }

    [Test]
    public async Task InstallMod_VanillaFilesNoBackups_CreatesVanillaBackup()
    {
        var fs = Init(x =>
        {
            x.InitFile().Data("original exe").Name("test.exe").In(GameRoot);
            x.InitFile().Data("original vpp").Name("archive.vpp_pc").In(GameData);
            x.InitFile().Data("mod exe").Name("test.exe").In(ModRoot);
            x.InitFile().Data("mod vpp").Name("archive.vpp_pc").In(ModRoot);

        });
        var expected = fs.Clone(x =>
        {
            x.InitFile().Data("original exe").Name("test.exe").In(VanillaRoot);
            x.InitFile().Data("original vpp").Name("archive.vpp_pc").In(VanillaData);
            x.InitFile().Data("mod exe").Name("test.exe").In(GameRoot);
            x.InitFile().Data("mod vpp").Name("archive.vpp_pc").In(GameData);
        });
        var hashes = Combine(new []{Hashes.Exe, Hashes.Data.Vpp});

        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var mod = new Mod
        {
            Id = ModId
        };

        var result = await manager.InstallMod(storage, mod, false, token);
        fs.ShouldHaveSameFilesAs(expected);
        result.Success.Should().BeTrue();
    }


/*
    [TestCase("/game/test.exe","/game/data/.syncfaction/Mod_22/test.exe", "/game/data/.syncfaction/.bak_vanilla/test.exe")]
    [TestCase("/game/data/test2.vpp_pc","/game/data/.syncfaction/Mod_22/test2.vpp_pc", "/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc")]
    [TestCase("/game/data/test2.vpp_pc","/game/data/.syncfaction/Mod_22/data/test2.vpp_pc", "/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc")]
    public async Task ApplyModExclusive_CopyOver(string gameFile, string modFile, string bakFile)
    {
        var originalData = "original";
        var moddedData = "modded";
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},

            {gameFile, new MockFileData(originalData)},
            {modFile, new MockFileData(moddedData)},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"}
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var mod = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallMod(storage, mod, false, token);
        result.Success.Should().BeTrue();
        fs.GetFile(gameFile).TextContents.Should().Be(moddedData);
        fs.GetFile(bakFile).TextContents.Should().Be(originalData);
    }

    [Test]
    public async Task ApplyModExclusive_RestoreFromBackup()
    {
        var gameFile ="/game/test.exe";
        var modFile="/game/data/.syncfaction/Mod_22/test.wtf";
        var bakFile = "/game/data/.syncfaction/.bak_vanilla/test.exe";
        var bakData = "from_backup";
        var originalData = "original";
        var moddedData = "modded";
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},

            {bakFile, new MockFileData(bakData)},
            {gameFile, new MockFileData(originalData)},
            {modFile, new MockFileData(moddedData)},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var mod = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallMod(storage, mod, false, token);
        result.Success.Should().BeFalse();
        fs.GetFile(gameFile).TextContents.Should().Be(bakData);
        fs.GetFile(bakFile).TextContents.Should().Be(bakData);
    }

    [Test]
    public async Task ApplyModExclusive_RestoreFromCommunityBackup()
    {
        var gameFile ="/game/test.exe";
        var modFile="/game/data/.syncfaction/Mod_22/test.wtf";
        var bakFile = "/game/data/.syncfaction/.bak_vanilla/test.exe";
        var communityBakFile = "/game/data/.syncfaction/.bak_community/test.exe";
        var bakData = "from_backup";
        var communityBakData = "from_community";
        var originalData = "original";
        var moddedData = "modded";
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {bakFile, new MockFileData(bakData)},
            {communityBakFile, new MockFileData(communityBakData)},
            {gameFile, new MockFileData(originalData)},
            {modFile, new MockFileData(moddedData)},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var mod = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallMod(storage, mod, false, token);
        result.Success.Should().BeFalse();
        fs.GetFile(gameFile).TextContents.Should().Be(communityBakData);
        fs.GetFile(bakFile).TextContents.Should().Be(bakData);
        fs.GetFile(communityBakFile).TextContents.Should().Be(communityBakData);
    }

    [Test]
    public async Task Apply_Mod()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_mod")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_mod")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var patch = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallMod(storage, patch, false, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_mod");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_mod");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").Should().BeNull();
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").Should().BeNull();
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [Test]
    public async Task Apply_Mod_Mod()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_11/test.exe", new MockFileData("exe_mod")},
            {"/game/data/.syncfaction/Mod_11/test2.vpp_pc", new MockFileData("vpp_mod")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_mod2")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_mod2")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);

        var mod = new Mod
        {
            Id = 11
        };
        var modResult = await manager.InstallMod(storage, mod, false, token);
        modResult.Success.Should().BeTrue();

        var mod2 = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallMod(storage, mod2, false, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_mod2");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_mod2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").Should().BeNull();
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").Should().BeNull();
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }
/*
    [Test]
    public async Task  Apply_Patch_Mod()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_11/test.exe", new MockFileData("exe_patch1")},
            {"/game/data/.syncfaction/Mod_11/test2.vpp_pc", new MockFileData("vpp_patch1")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_mod")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_mod")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);

        var patch1 = new Mod
        {
            Id = 11
        };
        var patch1Result = await manager.InstallUpdate(storage, patch1, token);
        patch1result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch1");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var patch2 = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallMod(storage, patch2, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_mod");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_mod");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [Test]
    public async Task Apply_Patch()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_patch")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var patch = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallUpdate(storage, patch, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [Test]
    public async Task Apply_Mod_Patch()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_11/test.exe", new MockFileData("exe_mod")},
            {"/game/data/.syncfaction/Mod_11/test2.vpp_pc", new MockFileData("vpp_mod")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_patch")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);

        var mod = new Mod
        {
            Id = 11
        };
        var modResult = await manager.InstallMod(storage, mod, false, token);
        modresult.Success.Should().BeTrue();

        var patch = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallUpdate(storage, patch, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [Test]
    public async Task Apply_Patch_Patch()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_11/test.exe", new MockFileData("exe_patch1")},
            {"/game/data/.syncfaction/Mod_11/test2.vpp_pc", new MockFileData("vpp_patch1")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_patch2")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_patch2")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);

        var patch1 = new Mod
        {
            Id = 11
        };
        var patch1Result = await manager.InstallUpdate(storage, patch1, token);
        patch1result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch1");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var patch2 = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallUpdate(storage, patch2, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch2");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch2");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [Test]
    public async Task Apply_Patch_Update()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_11/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_11/test2.vpp_pc", new MockFileData("vpp_patch")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_upd")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_upd")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);

        var patch1 = new Mod
        {
            Id = 11
        };
        var patchResult = await manager.InstallUpdate(storage, patch1, token);
        patchresult.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var upd = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallCommunityUpdateIncremental(storage, new List<IMod>(){upd}, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [Test]
    public async Task Apply_Patch_Update_Update()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_11/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_11/test2.vpp_pc", new MockFileData("vpp_patch")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_upd1")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_upd1")},
            {"/game/data/.syncfaction/Mod_33/test.exe", new MockFileData("exe_upd2")},
            {"/game/data/.syncfaction/Mod_33/test2.vpp_pc", new MockFileData("vpp_upd2")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);

        var patch1 = new Mod
        {
            Id = 11
        };
        var patchResult = await manager.InstallUpdate(storage, patch1, token);
        patchresult.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var upd1 = new Mod
        {
            Id = 22
        };

        var result1 = await manager.InstallCommunityUpdateIncremental(storage, new List<IMod>(){upd1}, token);
        result1.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var upd2 = new Mod
        {
            Id = 33
        };

        var result2 = await manager.InstallCommunityUpdateIncremental(storage, new List<IMod>(){upd2}, token);
        result2.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd2");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd2");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [Test]
    public async Task Apply_Patch_Update_Patch()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_11/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_11/test2.vpp_pc", new MockFileData("vpp_patch")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_upd1")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_upd1")},
            {"/game/data/.syncfaction/Mod_33/test.exe", new MockFileData("exe_patch2")},
            {"/game/data/.syncfaction/Mod_33/test2.vpp_pc", new MockFileData("vpp_patch2")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);

        var patch1 = new Mod
        {
            Id = 11
        };
        var patchResult = await manager.InstallUpdate(storage, patch1, token);
        patchresult.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var upd1 = new Mod
        {
            Id = 22
        };

        var result1 = await manager.InstallCommunityUpdateIncremental(storage, new List<IMod>(){upd1}, token);
        result1.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var patch2 = new Mod
        {
            Id = 33
        };

        var result2 = await manager.InstallUpdate(storage, patch2, token);
        result2.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch2");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch2");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [Test]
    public async Task Apply_Patch_Update_Mod()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_11/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_11/test2.vpp_pc", new MockFileData("vpp_patch")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_upd1")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_upd1")},
            {"/game/data/.syncfaction/Mod_33/test.exe", new MockFileData("exe_mod")},
            {"/game/data/.syncfaction/Mod_33/test2.vpp_pc", new MockFileData("vpp_mod")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);

        var patch1 = new Mod
        {
            Id = 11
        };
        var patchResult = await manager.InstallUpdate(storage, patch1, token);
        patchresult.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var upd1 = new Mod
        {
            Id = 22
        };

        var result1 = await manager.InstallCommunityUpdateIncremental(storage, new List<IMod>(){upd1}, token);
        result1.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var mod = new Mod
        {
            Id = 33
        };

        var result2 = await manager.InstallMod(storage, mod, false, token);
        result2.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_mod");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_mod");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [Test]
    public async Task Apply_Patch_Update_Mod_Update()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_11/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_11/test2.vpp_pc", new MockFileData("vpp_patch")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_upd1")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_upd1")},
            {"/game/data/.syncfaction/Mod_33/test.exe", new MockFileData("exe_mod")},
            {"/game/data/.syncfaction/Mod_33/test2.vpp_pc", new MockFileData("vpp_mod")},
            {"/game/data/.syncfaction/Mod_44/test.exe", new MockFileData("exe_upd2")},
            {"/game/data/.syncfaction/Mod_44/test2.vpp_pc", new MockFileData("vpp_upd2")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);

        var patch1 = new Mod
        {
            Id = 11
        };
        var patchResult = await manager.InstallUpdate(storage, patch1, token);
        patchresult.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var upd1 = new Mod
        {
            Id = 22
        };

        var result1 = await manager.InstallCommunityUpdateIncremental(storage, new List<IMod>(){upd1}, token);
        result1.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var mod = new Mod
        {
            Id = 33
        };

        var result2 = await manager.InstallMod(storage, mod, false, token);
        result2.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_mod");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_mod");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var upd2 = new Mod
        {
            Id = 44
        };

        var result3 = await manager.InstallCommunityUpdateIncremental(storage, new List<IMod>(){upd2}, token);
        result3.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd2");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd2");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [Test]
    public async Task Apply_Patch_Update_Mod_Patch()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_11/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_11/test2.vpp_pc", new MockFileData("vpp_patch")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_upd1")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_upd1")},
            {"/game/data/.syncfaction/Mod_33/test.exe", new MockFileData("exe_mod")},
            {"/game/data/.syncfaction/Mod_33/test2.vpp_pc", new MockFileData("vpp_mod")},
            {"/game/data/.syncfaction/Mod_44/test.exe", new MockFileData("exe_patch2")},
            {"/game/data/.syncfaction/Mod_44/test2.vpp_pc", new MockFileData("vpp_patch2")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);

        var patch1 = new Mod
        {
            Id = 11
        };
        var patchResult = await manager.InstallUpdate(storage, patch1, token);
        patchresult.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var upd1 = new Mod
        {
            Id = 22
        };

        var result1 = await manager.InstallCommunityUpdateIncremental(storage, new List<IMod>(){upd1}, token);
        result1.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var mod = new Mod
        {
            Id = 33
        };

        var result2 = await manager.InstallMod(storage, mod, false, token);
        result2.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_mod");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_mod");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var patch2 = new Mod
        {
            Id = 44
        };

        var result3 = await manager.InstallUpdate(storage, patch2, token);
        result3.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch2");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch2");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [Test]
    public async Task Apply_Patch_Update_Mod_Mod()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_11/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_11/test2.vpp_pc", new MockFileData("vpp_patch")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_upd1")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_upd1")},
            {"/game/data/.syncfaction/Mod_33/test.exe", new MockFileData("exe_mod1")},
            {"/game/data/.syncfaction/Mod_33/test2.vpp_pc", new MockFileData("vpp_mod1")},
            {"/game/data/.syncfaction/Mod_44/test.exe", new MockFileData("exe_mod2")},
            {"/game/data/.syncfaction/Mod_44/test2.vpp_pc", new MockFileData("vpp_mod2")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);

        var patch1 = new Mod
        {
            Id = 11
        };
        var patchResult = await manager.InstallUpdate(storage, patch1, token);
        patchresult.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var upd1 = new Mod
        {
            Id = 22
        };

        var result1 = await manager.InstallCommunityUpdateIncremental(storage, new List<IMod>(){upd1}, token);
        result1.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var mod = new Mod
        {
            Id = 33
        };

        var result2 = await manager.InstallMod(storage, mod, false, token);
        result2.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_mod1");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_mod1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var mod2 = new Mod
        {
            Id = 44
        };

        var result3 = await manager.InstallMod(storage, mod2, token);
        result3.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_mod2");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_mod2");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd1");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Mod_Restore(bool toVanilla)
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_mod")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_mod")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var patch = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallMod(storage, patch, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_mod");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_mod");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").Should().BeNull();
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").Should().BeNull();
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        await manager.Restore(storage, toVanilla, token);
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_original");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").Should().BeNull();
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").Should().BeNull();
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Patch_Restore(bool toVanilla)
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_patch")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var patch = new Mod
        {
            Id = 22
        };

        var result = await manager.InstallUpdate(storage, patch, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        await manager.Restore(storage, toVanilla, token);
        if (toVanilla)
        {
            fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_original");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
        }
        else
        {
            fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
            fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Patch_Mod_Restore(bool toVanilla)
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_patch")},
            {"/game/data/.syncfaction/Mod_33/test.exe", new MockFileData("exe_mod")},
            {"/game/data/.syncfaction/Mod_33/test2.vpp_pc", new MockFileData("vpp_mod")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var patch = new Mod
        {
            Id = 22
        };
        var result = await manager.InstallUpdate(storage, patch, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var mod = new Mod
        {
            Id = 33
        };
        var result2 = await manager.InstallMod(storage, mod, false, token);
        result2.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_mod");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_mod");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        await manager.Restore(storage, toVanilla, token);
        if (toVanilla)
        {
            fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_original");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
        }
        else
        {
            fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
            fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Patch_Update_Restore(bool toVanilla)
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_patch")},
            {"/game/data/.syncfaction/Mod_33/test.exe", new MockFileData("exe_upd")},
            {"/game/data/.syncfaction/Mod_33/test2.vpp_pc", new MockFileData("vpp_upd")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var patch = new Mod
        {
            Id = 22
        };
        var result = await manager.InstallUpdate(storage, patch, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var upd = new Mod
        {
            Id = 33
        };
        var result2 = await manager.InstallCommunityUpdateIncremental(storage, new List<IMod>(){upd}, token);
        result2.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        await manager.Restore(storage, toVanilla, token);
        if (toVanilla)
        {
            fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_original");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
        }
        else
        {
            fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd");
            fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Patch_Update_Mod_Restore(bool toVanilla)
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_community/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("exe_original")},
            {"/game/data/test2.vpp_pc", new MockFileData("vpp_original")},
            {"/game/data/.syncfaction/Mod_22/test.exe", new MockFileData("exe_patch")},
            {"/game/data/.syncfaction/Mod_22/test2.vpp_pc", new MockFileData("vpp_patch")},
            {"/game/data/.syncfaction/Mod_33/test.exe", new MockFileData("exe_upd")},
            {"/game/data/.syncfaction/Mod_33/test2.vpp_pc", new MockFileData("vpp_upd")},
            {"/game/data/.syncfaction/Mod_44/test.exe", new MockFileData("exe_mod")},
            {"/game/data/.syncfaction/Mod_44/test2.vpp_pc", new MockFileData("vpp_mod")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"},
            {"data/test2.vpp_pc", "123"},
        }.ToImmutableDictionary();
        var fs = new MockFileSystem(fsData);
        var storage = new GameStorage(gameDir, fs, hashes, log);
        var manager = new FileManager(modTools, log);
        var patch = new Mod
        {
            Id = 22
        };
        var result = await manager.InstallUpdate(storage, patch, token);
        result.Success.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_patch");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var upd = new Mod
        {
            Id = 33
        };
        var result2 = await manager.InstallCommunityUpdateIncremental(storage, new List<IMod>(){upd}, token);
        result2.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        var mod = new Mod
        {
            Id = 44
        };
        var result3 = await manager.InstallMod(storage, mod, false, token);
        result3.Should().BeTrue();
        fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_mod");
        fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_mod");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd");
        fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
        fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");

        await manager.Restore(storage, toVanilla, token);
        if (toVanilla)
        {
            fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_original");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
        }
        else
        {
            fs.GetFile("/game/test.exe").TextContents.Should().Be("exe_upd");
            fs.GetFile("/game/data/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test.exe").TextContents.Should().Be("exe_upd");
            fs.GetFile("/game/data/.syncfaction/.bak_community/test2.vpp_pc").TextContents.Should().Be("vpp_upd");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test.exe").TextContents.Should().Be("exe_original");
            fs.GetFile("/game/data/.syncfaction/.bak_vanilla/test2.vpp_pc").TextContents.Should().Be("vpp_original");
        }
    }
    */
}
