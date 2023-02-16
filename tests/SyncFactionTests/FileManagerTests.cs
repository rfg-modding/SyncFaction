using System.Collections.Immutable;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.ModManager;

namespace SyncFactionTests;

/*
public class FileManagerTests
{
    private ILogger<FileManager> log = new NullLogger<FileManager>();
    private CancellationToken token = CancellationToken.None;
    private string gameDir = "/game";

    private readonly ModTools modTools = new(new NullLogger<ModTools>());

    [Test]
    public async Task ApplyModExclusive_EmptyMod_False()
    {
        var modMock = new Mock<IMod>();
        var dirMock = new Mock<IDirectoryInfo>();
        dirMock.Setup(x => x.EnumerateFiles(It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(Enumerable.Empty<IFileInfo>());
        var storageMock = new Mock<IGameStorage>();
        storageMock.Setup(x => x.GetModDir(It.IsAny<IMod>()))
            .Returns(dirMock.Object);

        var mod = modMock.Object;
        var storage = storageMock.Object;
        var manager = new FileManager(modTools, log);

        var result = await manager.InstallMod(storage, mod, false, token);
        result.Success.Should().BeFalse();
    }

    [Test]
    public async Task ApplyModExclusive_NewFile_True()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},

            {"/game/data/.syncfaction/Mod_22/unknown_file.wtf", new MockFileData("test")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"}
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
    }

    [Test]
    public async Task ApplyModExclusive_UnsupportedFile_False()
    {
        var fsData = new Dictionary<string, MockFileData>
        {
            {"/game/.keep", new MockFileData("i am directory")},
            {"/game/data/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.keep", new MockFileData("i am directory")},
            {"/game/data/.syncfaction/.bak_vanilla/.keep", new MockFileData("i am directory")},

            {"/game/test.exe", new MockFileData("test")},
            {"/game/data/.syncfaction/Mod_22/test.unknown_mod_format", new MockFileData("test")},
        };
        var hashes = new Dictionary<string, string>()
        {
            {"test.exe", "123"}
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
    }

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
}
*/
