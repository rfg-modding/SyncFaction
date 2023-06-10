using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.ModManager;
using SyncFaction.Packer;
using static SyncFactionTests.Fs;

namespace SyncFactionTests;

public class FileManagerTests
{
    private ILogger<FileManager> log = new NullLogger<FileManager>();
    private CancellationToken token = CancellationToken.None;
    private static Mock<IVppArchiver> archiverMock = new();

    private readonly ModTools modTools = new(new NullLogger<ModTools>());
    private readonly ModInstaller modInstaller = new(archiverMock.Object, null, null, new NullLogger<ModInstaller>());

    private static void AddDefaultFiles(MockFileSystem x)
    {
            // stock
            x.MkFile(Game.Root, File.Exe, Data.Orig);
            x.MkFile(Game.Data, File.Vpp, Data.Orig);
            x.MkFile(Game.REtc, File.Dll, Data.Orig);
            x.MkFile(Game.DEtc, File.Etc, Data.Orig);

            // unmanaged
            x.MkFile(Game.Root, File.Vpp, Data.Orig);
            x.MkFile(Game.Data, File.Dll, Data.Orig);
            x.MkFile(Game.REtc, File.Etc, Data.Orig);
            x.MkFile(Game.DEtc, File.Exe, Data.Orig);

            // mod 1
            x.MkFile(Mod1.Root, File.Exe, Data.Mod1);
            x.MkFile(Mod1.Data, File.Vpp, Data.Mod1);
            x.MkFile(Mod1.REtc, File.Dll, Data.Mod1);
            x.MkFile(Mod1.DEtc, File.Etc, Data.Mod1);

            x.MkFile(Mod1.Root, File.Vpp, Data.Mod1);
            x.MkFile(Mod1.Data, File.Dll, Data.Mod1);
            x.MkFile(Mod1.REtc, File.Etc, Data.Mod1);
            x.MkFile(Mod1.DEtc, File.Exe, Data.Mod1);

            // mod 2
            x.MkFile(Mod2.Root, File.Dll, Data.Mod2);
            x.MkFile(Mod2.Data, File.Etc, Data.Mod2);
            x.MkFile(Mod2.REtc, File.Exe, Data.Mod2);
            x.MkFile(Mod2.DEtc, File.Vpp, Data.Mod2);

            x.MkFile(Mod2.Root, File.Etc, Data.Mod2);
            x.MkFile(Mod2.Data, File.Exe, Data.Mod2);
            x.MkFile(Mod2.REtc, File.Vpp, Data.Mod2);
            x.MkFile(Mod2.DEtc, File.Dll, Data.Mod2);

            // patch 1
            x.MkFile(Pch1.Root, File.Exe, Data.Pch1);
            x.MkFile(Pch1.Data, File.Vpp, Data.Pch1);
            x.MkFile(Pch1.REtc, File.Dll, Data.Pch1);
            x.MkFile(Pch1.DEtc, File.Etc, Data.Pch1);

            x.MkFile(Pch1.Root, File.Vpp, Data.Pch1);
            x.MkFile(Pch1.Data, File.Dll, Data.Pch1);
            x.MkFile(Pch1.REtc, File.Etc, Data.Pch1);
            x.MkFile(Pch1.DEtc, File.Exe, Data.Pch1);

            // patch 2
            x.MkFile(Pch2.Root, File.Dll, Data.Pch2);
            x.MkFile(Pch2.Data, File.Etc, Data.Pch2);
            x.MkFile(Pch2.REtc, File.Exe, Data.Pch2);
            x.MkFile(Pch2.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(Pch2.Root, File.Etc, Data.Pch2);
            x.MkFile(Pch2.Data, File.Exe, Data.Pch2);
            x.MkFile(Pch2.REtc, File.Vpp, Data.Pch2);
            x.MkFile(Pch2.DEtc, File.Dll, Data.Pch2);
    }

    private void CheckDefaultFileKinds(MockFileSystem fs, IGameStorage storage)
    {
        fs.GetGameFile(storage, Game.Root, Names.Exe).Kind.Should().Be(FileKind.Stock);
        fs.GetGameFile(storage, Game.Data, Names.Vpp).Kind.Should().Be(FileKind.Stock);
        fs.GetGameFile(storage, Game.REtc, Names.Dll).Kind.Should().Be(FileKind.Stock);
        fs.GetGameFile(storage, Game.DEtc, Names.Etc).Kind.Should().Be(FileKind.Stock);

        fs.GetGameFile(storage, Game.Root, Names.Vpp).Kind.Should().Be(FileKind.Unmanaged);
        fs.GetGameFile(storage, Game.Data, Names.Dll).Kind.Should().Be(FileKind.Unmanaged);
        fs.GetGameFile(storage, Game.REtc, Names.Etc).Kind.Should().Be(FileKind.Unmanaged);
        fs.GetGameFile(storage, Game.DEtc, Names.Exe).Kind.Should().Be(FileKind.Unmanaged);

        fs.GetGameFile(storage, Game.Root, Names.Dll).Kind.Should().Be(FileKind.FromPatch);
        fs.GetGameFile(storage, Game.Data, Names.Etc).Kind.Should().Be(FileKind.FromPatch);
        fs.GetGameFile(storage, Game.REtc, Names.Exe).Kind.Should().Be(FileKind.FromPatch);
        fs.GetGameFile(storage, Game.DEtc, Names.Vpp).Kind.Should().Be(FileKind.FromPatch);

        fs.GetGameFile(storage, Game.Root, Names.Etc).Kind.Should().Be(FileKind.FromMod);
        fs.GetGameFile(storage, Game.Data, Names.Exe).Kind.Should().Be(FileKind.FromMod);
        fs.GetGameFile(storage, Game.REtc, Names.Vpp).Kind.Should().Be(FileKind.FromMod);
        fs.GetGameFile(storage, Game.DEtc, Names.Dll).Kind.Should().Be(FileKind.FromMod);
    }

    [SetUp]
    public void SetUp()
    {
    }

    [Test]
    public async Task ForgetUpdates_Works()
    {
        var fs = Init();
        var hashes = Hashes.Empty;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
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
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
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
        var hashes = Hashes.ExeDll;
        fs.DirectoryInfo.New(Mod1.Root).Create();
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod = new Mod
        {
            Id = ModId1
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
            x.MkFile(Mod1.Root, File.Etc, Data.Mod1);

        });
        var expected = fs.Clone(x =>
        {
            x.MkFile(Game.Root, File.Etc, Data.Mod1);
            x.MkFile(Mngd.Root, File.Etc, Data.None);
        });
        var hashes = Hashes.ExeDll;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod = new Mod
        {
            Id = ModId1
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
            x.TestFile().Data("1").Name("unsupported1.jpg").In(Mod1.Root);
            x.TestFile().Data("2").Name("unsupported2.jPeG").In(Mod1.Data);
            x.TestFile().Data("3").Name("unsupported3.zip").In(Mod1.REtc);
            x.TestFile().Data("4").Name("unsupported4.7Z").In(Mod1.DEtc);
            x.TestFile().Data("5").Name("unsupported5.Txt").In(Mod1.Root);
            x.TestFile().Data("6").Name("unsupported6.PNG").In(Mod1.Root);
            x.TestFile().Data("7").Name(".mod_unsupported7.vpp_pc").In(Mod1.Root);
            x.TestFile().Data("8").Name(".mod_unsupported8.exe").In(Mod1.Data);
        });
        var expected = fs.Clone(x =>
        {
        });
        var hashes = Hashes.ExeDll;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod = new Mod
        {
            Id = ModId1
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
            x.MkFile(Game.Root, File.Exe, Data.Orig);
            x.MkFile(Game.Data, File.Vpp, Data.Orig);
            x.MkFile(Mod1.Root, File.Exe, Data.Mod1);
            x.MkFile(Mod1.Root, File.Vpp, Data.Mod1);
        });
        var expected = fs.Clone(x =>
        {
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
        });
        var hashes = Combine(new []{Hashes.ExeDll, Hashes.Data.Vpp});

        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod = new Mod
        {
            Id = ModId1
        };

        var result = await manager.InstallMod(storage, mod, false, token);
        fs.ShouldHaveSameFilesAs(expected);
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task InstallMod_VanillaFilesWithBackups_Overwrites()
    {
        var fs = Init(x =>
        {
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(Game.Root, File.Exe, Data.Drty);
            x.MkFile(Game.Data, File.Vpp, Data.Drty);
            x.MkFile(Mod1.Root, File.Exe, Data.Mod1);
            x.MkFile(Mod1.Root, File.Vpp, Data.Mod1);

        });
        var expected = fs.Clone(x =>
        {
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
        });
        var hashes = Combine(new []{Hashes.ExeDll, Hashes.Data.Vpp});

        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod = new Mod
        {
            Id = ModId1
        };

        var result = await manager.InstallMod(storage, mod, false, token);
        fs.ShouldHaveSameFilesAs(expected);
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task InstallMod_WithSomeVanillaFiles_KeepsModifiedOthers()
    {
        var fs = Init(x =>
        {
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(Game.Root, File.Exe, Data.Drty);
            x.MkFile(Game.Data, File.Vpp, Data.Drty);
            x.MkFile(Game.Root, File.Dll, Data.Orig);
            x.MkFile(Mod1.Root, File.Dll, Data.Mod1);
        });
        var expected = fs.Clone(x =>
        {
            x.MkFile(BakV.Root, File.Dll, Data.Orig);
            x.MkFile(Game.Root, File.Dll, Data.Mod1);
        });
        var hashes = Combine(new []{Hashes.ExeDll, Hashes.Data.Vpp});

        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod = new Mod
        {
            Id = ModId1
        };

        var result = await manager.InstallMod(storage, mod, false, token);
        fs.ShouldHaveSameFilesAs(expected);
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task InstallMod_InstallMod_Overwrites()
    {
        var fs = Init(x =>
        {
            x.MkFile(Game.Root, File.Exe, Data.Orig);
            x.MkFile(Game.Data, File.Vpp, Data.Orig);
            x.MkFile(Mod1.Root, File.Exe, Data.Mod1);
            x.MkFile(Mod1.Data, File.Vpp, Data.Mod1);
            x.MkFile(Mod2.Root, File.Exe, Data.Mod2);
        });
        var expected = fs.Clone(x =>
        {
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(Game.Root, File.Exe, Data.Mod2);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
        });
        var hashes = Combine(new []{Hashes.ExeDll, Hashes.Data.Vpp});
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};

        var result1 = await manager.InstallMod(storage, mod1, false, token);
        var result2 = await manager.InstallMod(storage, mod2, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    [Test]
    public async Task InstallMod_WithClutter_Works()
    {
        var fs = Init(x =>
        {
            // stock
            x.MkFile(Game.Root, File.Exe, Data.Orig);
            x.MkFile(Game.Data, File.Vpp, Data.Orig);
            x.MkFile(Game.REtc, File.Dll, Data.Orig);
            x.MkFile(Game.DEtc, File.Etc, Data.Orig);

            // unmanaged
            x.MkFile(Game.Root, File.Vpp, Data.Orig);
            x.MkFile(Game.Data, File.Dll, Data.Orig);
            x.MkFile(Game.REtc, File.Etc, Data.Orig);
            x.MkFile(Game.DEtc, File.Exe, Data.Orig);

            // mod
            x.MkFile(Mod1.Root, File.Etc, Data.Mod1);
            x.MkFile(Mod1.Data, File.Exe, Data.Mod1);
            x.MkFile(Mod1.REtc, File.Vpp, Data.Mod1);
            x.MkFile(Mod1.DEtc, File.Dll, Data.Mod1);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Etc, Data.Mod1);
            x.MkFile(Game.Data, File.Exe, Data.Mod1);
            x.MkFile(Game.REtc, File.Vpp, Data.Mod1);
            x.MkFile(Game.DEtc, File.Dll, Data.Mod1);

            // managed
            x.MkFile(Mngd.Root, File.Etc, Data.None);
            x.MkFile(Mngd.Data, File.Exe, Data.None);
            x.MkFile(Mngd.REtc, File.Vpp, Data.None);
            x.MkFile(Mngd.DEtc, File.Dll, Data.None);
        });
        var hashes = Combine(new []{Hashes.ExeDll, Hashes.Data.Vpp});
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};

        var result1 = await manager.InstallMod(storage, mod1, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
    }

    [Test]
    public async Task InstallPatch_WithClutter_Works()
    {
        var fs = Init(x =>
        {
            // stock
            x.MkFile(Game.Root, File.Exe, Data.Orig);
            x.MkFile(Game.Data, File.Vpp, Data.Orig);
            x.MkFile(Game.REtc, File.Dll, Data.Orig);
            x.MkFile(Game.DEtc, File.Etc, Data.Orig);

            // unmanaged
            x.MkFile(Game.Root, File.Vpp, Data.Orig);
            x.MkFile(Game.Data, File.Dll, Data.Orig);
            x.MkFile(Game.REtc, File.Etc, Data.Orig);
            x.MkFile(Game.DEtc, File.Exe, Data.Orig);

            // patch
            x.MkFile(Mod1.Root, File.Etc, Data.Mod1);
            x.MkFile(Mod1.Data, File.Exe, Data.Mod1);
            x.MkFile(Mod1.REtc, File.Vpp, Data.Mod1);
            x.MkFile(Mod1.DEtc, File.Dll, Data.Mod1);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Etc, Data.Mod1);
            x.MkFile(Game.Data, File.Exe, Data.Mod1);
            x.MkFile(Game.REtc, File.Vpp, Data.Mod1);
            x.MkFile(Game.DEtc, File.Dll, Data.Mod1);

            // bak_patch
            x.MkFile(BakP.Root, File.Etc, Data.Mod1);
            x.MkFile(BakP.Data, File.Exe, Data.Mod1);
            x.MkFile(BakP.REtc, File.Vpp, Data.Mod1);
            x.MkFile(BakP.DEtc, File.Dll, Data.Mod1);
        });
        var hashes = Combine(new []{Hashes.ExeDll, Hashes.Data.Vpp});
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};

        var result1 = await manager.InstallMod(storage, mod1, true, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
    }

    [Test]
    public async Task InstallPatch_InstallPatch_WithClutter_Works()
    {
        var fs = Init(x =>
        {
            // stock
            x.MkFile(Game.Root, File.Exe, Data.Orig);
            x.MkFile(Game.Data, File.Vpp, Data.Orig);
            x.MkFile(Game.REtc, File.Dll, Data.Orig);
            x.MkFile(Game.DEtc, File.Etc, Data.Orig);

            // unmanaged
            x.MkFile(Game.Root, File.Vpp, Data.Orig);
            x.MkFile(Game.Data, File.Dll, Data.Orig);
            x.MkFile(Game.REtc, File.Etc, Data.Orig);
            x.MkFile(Game.DEtc, File.Exe, Data.Orig);

            // patch1
            x.MkFile(Mod1.Root, File.Etc, Data.Mod1);
            x.MkFile(Mod1.Data, File.Exe, Data.Mod1);
            x.MkFile(Mod1.REtc, File.Vpp, Data.Mod1);
            x.MkFile(Mod1.DEtc, File.Dll, Data.Mod1);

            // patch2
            x.MkFile(Mod2.Root, File.Etc, Data.Mod2);
            x.MkFile(Mod2.Data, File.Exe, Data.Mod2);
            x.MkFile(Mod2.REtc, File.Vpp, Data.Mod2);
            x.MkFile(Mod2.DEtc, File.Dll, Data.Mod2);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Etc, Data.Mod2);
            x.MkFile(Game.Data, File.Exe, Data.Mod2);
            x.MkFile(Game.REtc, File.Vpp, Data.Mod2);
            x.MkFile(Game.DEtc, File.Dll, Data.Mod2);

            // bak_patch
            x.MkFile(BakP.Root, File.Etc, Data.Mod2);
            x.MkFile(BakP.Data, File.Exe, Data.Mod2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Mod2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Mod2);
        });
        var hashes = Combine(new []{Hashes.ExeDll, Hashes.Data.Vpp});
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};

        var result1 = await manager.InstallMod(storage, mod1, true, token);
        var result2 = await manager.InstallMod(storage, mod2, true, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    [Test]
    public async Task CheckAllFileKindsInAllFolders()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);

            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(Mngd.Root, File.Etc, Data.None);
            x.MkFile(Mngd.Data, File.Exe, Data.None);
            x.MkFile(Mngd.REtc, File.Vpp, Data.None);
            x.MkFile(Mngd.DEtc, File.Dll, Data.None);
        });

        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        CheckDefaultFileKinds(fs, storage);
    }


    [Test]
    public async Task Patch1()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Pch1);
            x.MkFile(Game.Data, File.Vpp, Data.Pch1);
            x.MkFile(Game.REtc, File.Dll, Data.Pch1);
            x.MkFile(Game.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Pch1);
            x.MkFile(Game.REtc, File.Etc, Data.Pch1);
            x.MkFile(Game.DEtc, File.Exe, Data.Pch1);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};

        var result1 = await manager.InstallMod(storage, pch1, true, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Pch1);
            x.MkFile(Game.Data, File.Vpp, Data.Pch1);
            x.MkFile(Game.REtc, File.Dll, Data.Pch1);
            x.MkFile(Game.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Pch1);
            x.MkFile(Game.REtc, File.Etc, Data.Pch1);
            x.MkFile(Game.DEtc, File.Exe, Data.Pch1);

            x.MkFile(Game.Root, File.Dll, Data.Pch2);
            x.MkFile(Game.Data, File.Etc, Data.Pch2);
            x.MkFile(Game.REtc, File.Exe, Data.Pch2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(Game.Root, File.Etc, Data.Pch2);
            x.MkFile(Game.Data, File.Exe, Data.Pch2);
            x.MkFile(Game.REtc, File.Vpp, Data.Pch2);
            x.MkFile(Game.DEtc, File.Dll, Data.Pch2);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};

        var result1 = await manager.InstallMod(storage, pch1, true, token);
        var result2 = await manager.InstallMod(storage, pch2, true, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_Mod1()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(Game.REtc, File.Dll, Data.Mod1);
            x.MkFile(Game.DEtc, File.Etc, Data.Mod1);

            //x.MkFile(Game.Root, File.Vpp, Data.Mod1);
            x.MkFile(Game.Data, File.Dll, Data.Mod1);
            x.MkFile(Game.REtc, File.Etc, Data.Mod1);
            x.MkFile(Game.DEtc, File.Exe, Data.Mod1);

            x.MkFile(Game.Root, File.Dll, Data.Pch2);
            x.MkFile(Game.Data, File.Etc, Data.Pch2);
            x.MkFile(Game.REtc, File.Exe, Data.Pch2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(Game.Root, File.Etc, Data.Pch2);
            x.MkFile(Game.Data, File.Exe, Data.Pch2);
            x.MkFile(Game.REtc, File.Vpp, Data.Pch2);
            x.MkFile(Game.DEtc, File.Dll, Data.Pch2);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

            // managed
            //x.MkFile(Mngd.Root, File.Vpp, Data.None);
            x.MkFile(Mngd.Data, File.Dll, Data.None);
            x.MkFile(Mngd.REtc, File.Etc, Data.None);
            x.MkFile(Mngd.DEtc, File.Exe, Data.None);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};
        var mod1 = new Mod {Id = ModId1};

        var result1 = await manager.InstallMod(storage, pch1, true, token);
        var result2 = await manager.InstallMod(storage, pch2, true, token);
        var result3 = await manager.InstallMod(storage, mod1, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_Mod1_Rollback()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Pch1);
            x.MkFile(Game.Data, File.Vpp, Data.Pch1);
            x.MkFile(Game.REtc, File.Dll, Data.Pch1);
            x.MkFile(Game.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Pch1);
            x.MkFile(Game.REtc, File.Etc, Data.Pch1);
            x.MkFile(Game.DEtc, File.Exe, Data.Pch1);

            x.MkFile(Game.Root, File.Dll, Data.Pch2);
            x.MkFile(Game.Data, File.Etc, Data.Pch2);
            x.MkFile(Game.REtc, File.Exe, Data.Pch2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(Game.Root, File.Etc, Data.Pch2);
            x.MkFile(Game.Data, File.Exe, Data.Pch2);
            x.MkFile(Game.REtc, File.Vpp, Data.Pch2);
            x.MkFile(Game.DEtc, File.Dll, Data.Pch2);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};
        var mod1 = new Mod {Id = ModId1};

        var result1 = await manager.InstallMod(storage, pch1, true, token);
        var result2 = await manager.InstallMod(storage, pch2, true, token);
        var result3 = await manager.InstallMod(storage, mod1, false, token);
        manager.Rollback(storage, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_Mod1_Rollback_RollbackToVanilla()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // unmanaged
            //x.MkFile(Game.Root, File.Vpp, Data.Orig).Delete();
            x.MkFile(Game.Data, File.Dll, Data.Orig).Delete();
            x.MkFile(Game.REtc, File.Etc, Data.Orig).Delete();
            x.MkFile(Game.DEtc, File.Exe, Data.Orig).Delete();

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};
        var mod1 = new Mod {Id = ModId1};

        var result1 = await manager.InstallMod(storage, pch1, true, token);
        var result2 = await manager.InstallMod(storage, pch2, true, token);
        var result3 = await manager.InstallMod(storage, mod1, false, token);
        manager.Rollback(storage, false, token);
        manager.Rollback(storage, true, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_Mod1_RollbackToVanilla()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // unmanaged
            //x.MkFile(Game.Root, File.Vpp, Data.Orig).Delete();
            x.MkFile(Game.Data, File.Dll, Data.Orig).Delete();
            x.MkFile(Game.REtc, File.Etc, Data.Orig).Delete();
            x.MkFile(Game.DEtc, File.Exe, Data.Orig).Delete();

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};
        var mod1 = new Mod {Id = ModId1};

        var result1 = await manager.InstallMod(storage, pch1, true, token);
        var result2 = await manager.InstallMod(storage, pch2, true, token);
        var result3 = await manager.InstallMod(storage, mod1, false, token);
        manager.Rollback(storage, true, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_Mod1_RollbackToVanilla_Rollback()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Pch1);
            x.MkFile(Game.Data, File.Vpp, Data.Pch1);
            x.MkFile(Game.REtc, File.Dll, Data.Pch1);
            x.MkFile(Game.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Pch1);
            x.MkFile(Game.REtc, File.Etc, Data.Pch1);
            x.MkFile(Game.DEtc, File.Exe, Data.Pch1);

            x.MkFile(Game.Root, File.Dll, Data.Pch2);
            x.MkFile(Game.Data, File.Etc, Data.Pch2);
            x.MkFile(Game.REtc, File.Exe, Data.Pch2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(Game.Root, File.Etc, Data.Pch2);
            x.MkFile(Game.Data, File.Exe, Data.Pch2);
            x.MkFile(Game.REtc, File.Vpp, Data.Pch2);
            x.MkFile(Game.DEtc, File.Dll, Data.Pch2);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};
        var mod1 = new Mod {Id = ModId1};

        var result1 = await manager.InstallMod(storage, pch1, true, token);
        var result2 = await manager.InstallMod(storage, pch2, true, token);
        var result3 = await manager.InstallMod(storage, mod1, false, token);
        manager.Rollback(storage, true, token);
        manager.Rollback(storage, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_Mod1_Mod2()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(Game.REtc, File.Dll, Data.Mod1);
            x.MkFile(Game.DEtc, File.Etc, Data.Mod1);

            //x.MkFile(Game.Root, File.Vpp, Data.Mod1);
            x.MkFile(Game.Data, File.Dll, Data.Mod1);
            x.MkFile(Game.REtc, File.Etc, Data.Mod1);
            x.MkFile(Game.DEtc, File.Exe, Data.Mod1);

            x.MkFile(Game.Root, File.Dll, Data.Mod2);
            x.MkFile(Game.Data, File.Etc, Data.Mod2);
            x.MkFile(Game.REtc, File.Exe, Data.Mod2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Mod2);

            x.MkFile(Game.Root, File.Etc, Data.Mod2);
            x.MkFile(Game.Data, File.Exe, Data.Mod2);
            x.MkFile(Game.REtc, File.Vpp, Data.Mod2);
            x.MkFile(Game.DEtc, File.Dll, Data.Mod2);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

            // managed
            //x.MkFile(Mngd.Root, File.Vpp, Data.None);
            x.MkFile(Mngd.Data, File.Dll, Data.None);
            x.MkFile(Mngd.REtc, File.Etc, Data.None);
            x.MkFile(Mngd.DEtc, File.Exe, Data.None);

            x.MkFile(Mngd.Root, File.Dll, Data.None);
            x.MkFile(Mngd.Data, File.Etc, Data.None);
            x.MkFile(Mngd.REtc, File.Exe, Data.None);
            x.MkFile(Mngd.DEtc, File.Vpp, Data.None);

            x.MkFile(Mngd.Root, File.Etc, Data.None);
            x.MkFile(Mngd.Data, File.Exe, Data.None);
            x.MkFile(Mngd.REtc, File.Vpp, Data.None);
            x.MkFile(Mngd.DEtc, File.Dll, Data.None);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};

        var result1 = await manager.InstallMod(storage, pch1, true, token);
        var result2 = await manager.InstallMod(storage, pch2, true, token);
        var result3 = await manager.InstallMod(storage, mod1, false, token);
        var result4 = await manager.InstallMod(storage, mod2, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
        result4.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_Mod1_Mod2_Rollback()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Pch1);
            x.MkFile(Game.Data, File.Vpp, Data.Pch1);
            x.MkFile(Game.REtc, File.Dll, Data.Pch1);
            x.MkFile(Game.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Pch1);
            x.MkFile(Game.REtc, File.Etc, Data.Pch1);
            x.MkFile(Game.DEtc, File.Exe, Data.Pch1);

            x.MkFile(Game.Root, File.Dll, Data.Pch2);
            x.MkFile(Game.Data, File.Etc, Data.Pch2);
            x.MkFile(Game.REtc, File.Exe, Data.Pch2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(Game.Root, File.Etc, Data.Pch2);
            x.MkFile(Game.Data, File.Exe, Data.Pch2);
            x.MkFile(Game.REtc, File.Vpp, Data.Pch2);
            x.MkFile(Game.DEtc, File.Dll, Data.Pch2);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};

        var result1 = await manager.InstallMod(storage, pch1, true, token);
        var result2 = await manager.InstallMod(storage, pch2, true, token);
        var result3 = await manager.InstallMod(storage, mod1, false, token);
        var result4 = await manager.InstallMod(storage, mod2, false, token);
        manager.Rollback(storage, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
        result4.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_Mod1_Mod2_Rollback_RollbackToVanilla()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // unmanaged
            //x.MkFile(Game.Root, File.Vpp, Data.Orig).Delete();
            x.MkFile(Game.Data, File.Dll, Data.Orig).Delete();
            x.MkFile(Game.REtc, File.Etc, Data.Orig).Delete();
            x.MkFile(Game.DEtc, File.Exe, Data.Orig).Delete();

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};

        var result1 = await manager.InstallMod(storage, pch1, true, token);
        var result2 = await manager.InstallMod(storage, pch2, true, token);
        var result3 = await manager.InstallMod(storage, mod1, false, token);
        var result4 = await manager.InstallMod(storage, mod2, false, token);
        manager.Rollback(storage, false, token);
        manager.Rollback(storage, true, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
        result4.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_Mod1_Mod2_RollbackToVanilla()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // unmanaged
            //x.MkFile(Game.Root, File.Vpp, Data.Orig).Delete();
            x.MkFile(Game.Data, File.Dll, Data.Orig).Delete();
            x.MkFile(Game.REtc, File.Etc, Data.Orig).Delete();
            x.MkFile(Game.DEtc, File.Exe, Data.Orig).Delete();

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};

        var result1 = await manager.InstallMod(storage, pch1, true, token);
        var result2 = await manager.InstallMod(storage, pch2, true, token);
        var result3 = await manager.InstallMod(storage, mod1, false, token);
        var result4 = await manager.InstallMod(storage, mod2, false, token);
        manager.Rollback(storage, true, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
        result4.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_Mod1_Mod2_RollbackToVanilla_Rollback()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Pch1);
            x.MkFile(Game.Data, File.Vpp, Data.Pch1);
            x.MkFile(Game.REtc, File.Dll, Data.Pch1);
            x.MkFile(Game.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Pch1);
            x.MkFile(Game.REtc, File.Etc, Data.Pch1);
            x.MkFile(Game.DEtc, File.Exe, Data.Pch1);

            x.MkFile(Game.Root, File.Dll, Data.Pch2);
            x.MkFile(Game.Data, File.Etc, Data.Pch2);
            x.MkFile(Game.REtc, File.Exe, Data.Pch2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(Game.Root, File.Etc, Data.Pch2);
            x.MkFile(Game.Data, File.Exe, Data.Pch2);
            x.MkFile(Game.REtc, File.Vpp, Data.Pch2);
            x.MkFile(Game.DEtc, File.Dll, Data.Pch2);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};

        var result1 = await manager.InstallMod(storage, pch1, true, token);
        var result2 = await manager.InstallMod(storage, pch2, true, token);
        var result3 = await manager.InstallMod(storage, mod1, false, token);
        var result4 = await manager.InstallMod(storage, mod2, false, token);
        manager.Rollback(storage, true, token);
        manager.Rollback(storage, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
        result4.Success.Should().BeTrue();
    }


    [Test]
    public async Task Mod1_()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(Game.REtc, File.Dll, Data.Mod1);
            x.MkFile(Game.DEtc, File.Etc, Data.Mod1);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Mod1);
            x.MkFile(Game.REtc, File.Etc, Data.Mod1);
            x.MkFile(Game.DEtc, File.Exe, Data.Mod1);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // managed
            //x.MkFile(Mngd.Root, File.Vpp, Data.None);
            x.MkFile(Mngd.Data, File.Dll, Data.None);
            x.MkFile(Mngd.REtc, File.Etc, Data.None);
            x.MkFile(Mngd.DEtc, File.Exe, Data.None);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};

        var result1 = await manager.InstallMod(storage, mod1, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
    }

    [Test]
    public async Task Mod1_Rollback()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            //x.MkFile(Game.Root, File.Vpp, Data.Orig).Delete();
            x.MkFile(Game.Data, File.Dll, Data.Orig).Delete();
            x.MkFile(Game.REtc, File.Etc, Data.Orig).Delete();
            x.MkFile(Game.DEtc, File.Exe, Data.Orig).Delete();
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};

        var result1 = await manager.InstallMod(storage, mod1, false, token);
        manager.Rollback(storage, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
    }

    [Test]
    public async Task Mod1_RollbackToVanilla()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            //x.MkFile(Game.Root, File.Vpp, Data.Orig).Delete();
            x.MkFile(Game.Data, File.Dll, Data.Orig).Delete();
            x.MkFile(Game.REtc, File.Etc, Data.Orig).Delete();
            x.MkFile(Game.DEtc, File.Exe, Data.Orig).Delete();
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};

        var result1 = await manager.InstallMod(storage, mod1, false, token);
        manager.Rollback(storage, true, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
    }

    [Test]
    public async Task Mod1_Mod2()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(Game.REtc, File.Dll, Data.Mod1);
            x.MkFile(Game.DEtc, File.Etc, Data.Mod1);

            x.MkFile(Game.Root, File.Dll, Data.Mod2);
            x.MkFile(Game.Data, File.Etc, Data.Mod2);
            x.MkFile(Game.REtc, File.Exe, Data.Mod2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Mod2);

            x.MkFile(Game.Root, File.Etc, Data.Mod2);
            x.MkFile(Game.Data, File.Exe, Data.Mod2);
            x.MkFile(Game.REtc, File.Vpp, Data.Mod2);
            x.MkFile(Game.DEtc, File.Dll, Data.Mod2);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Mod1);
            x.MkFile(Game.REtc, File.Etc, Data.Mod1);
            x.MkFile(Game.DEtc, File.Exe, Data.Mod1);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // managed
            //x.MkFile(Mngd.Root, File.Vpp, Data.None);
            x.MkFile(Mngd.Data, File.Dll, Data.None);
            x.MkFile(Mngd.REtc, File.Etc, Data.None);
            x.MkFile(Mngd.DEtc, File.Exe, Data.None);

            x.MkFile(Mngd.Root, File.Dll, Data.None);
            x.MkFile(Mngd.Data, File.Etc, Data.None);
            x.MkFile(Mngd.REtc, File.Exe, Data.None);
            x.MkFile(Mngd.DEtc, File.Vpp, Data.None);

            x.MkFile(Mngd.Root, File.Etc, Data.None);
            x.MkFile(Mngd.Data, File.Exe, Data.None);
            x.MkFile(Mngd.REtc, File.Vpp, Data.None);
            x.MkFile(Mngd.DEtc, File.Dll, Data.None);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};

        var result1 = await manager.InstallMod(storage, mod1, false, token);
        var result2 = await manager.InstallMod(storage, mod2, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    [Test]
    public async Task Mod1_Mod2_Rollback()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // unmanaged
            //x.MkFile(Game.Root, File.Vpp, Data.Orig).Delete();
            x.MkFile(Game.Data, File.Dll, Data.Orig).Delete();
            x.MkFile(Game.REtc, File.Etc, Data.Orig).Delete();
            x.MkFile(Game.DEtc, File.Exe, Data.Orig).Delete();

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};

        var result1 = await manager.InstallMod(storage, mod1, false, token);
        var result2 = await manager.InstallMod(storage, mod2, false, token);
        manager.Rollback(storage, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    [Test]
    public async Task Mod1_Mod2_RollbackToVanilla()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // unmanaged
            //x.MkFile(Game.Root, File.Vpp, Data.Orig).Delete();
            x.MkFile(Game.Data, File.Dll, Data.Orig).Delete();
            x.MkFile(Game.REtc, File.Etc, Data.Orig).Delete();
            x.MkFile(Game.DEtc, File.Exe, Data.Orig).Delete();

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};

        var result1 = await manager.InstallMod(storage, mod1, false, token);
        var result2 = await manager.InstallMod(storage, mod2, false, token);
        manager.Rollback(storage, true, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    [Test]
    public async Task Mod1_Patch1()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(Game.REtc, File.Dll, Data.Mod1);
            x.MkFile(Game.DEtc, File.Etc, Data.Mod1);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Mod1);
            x.MkFile(Game.REtc, File.Etc, Data.Mod1);
            x.MkFile(Game.DEtc, File.Exe, Data.Mod1);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // managed
            //x.MkFile(Mngd.Root, File.Vpp, Data.None);
            x.MkFile(Mngd.Data, File.Dll, Data.None);
            x.MkFile(Mngd.REtc, File.Etc, Data.None);
            x.MkFile(Mngd.DEtc, File.Exe, Data.None);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var pch1 = new Mod {Id = PchId1};

        var result1 = await manager.InstallMod(storage, mod1, false, token);
        var result2 = await manager.InstallUpdate(storage, new List<IMod> {pch1}, false, new List<IMod>{mod1}, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    [Test]
    public async Task Mod1_Patch1_Rollback()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Pch1);
            x.MkFile(Game.Data, File.Vpp, Data.Pch1);
            x.MkFile(Game.REtc, File.Dll, Data.Pch1);
            x.MkFile(Game.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Pch1);
            x.MkFile(Game.REtc, File.Etc, Data.Pch1);
            x.MkFile(Game.DEtc, File.Exe, Data.Pch1);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var pch1 = new Mod {Id = PchId1};

        var result1 = await manager.InstallMod(storage, mod1, false, token);
        var result2 = await manager.InstallUpdate(storage, new List<IMod> {pch1}, false, new List<IMod>{mod1}, token);
        manager.Rollback(storage, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    [Test]
    public async Task Mod1_Patch1_RollbackToVanilla()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Orig);
            x.MkFile(Game.Data, File.Vpp, Data.Orig);
            x.MkFile(Game.REtc, File.Dll, Data.Orig);
            x.MkFile(Game.DEtc, File.Etc, Data.Orig);

            //x.MkFile(Game.Root, File.Vpp, Data.Orig).Delete();
            x.MkFile(Game.Data, File.Dll, Data.Orig).Delete();
            x.MkFile(Game.REtc, File.Etc, Data.Orig).Delete();
            x.MkFile(Game.DEtc, File.Exe, Data.Orig).Delete();

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var pch1 = new Mod {Id = PchId1};

        var result1 = await manager.InstallMod(storage, mod1, false, token);
        var result2 = await manager.InstallUpdate(storage, new List<IMod> {pch1}, false, new List<IMod>{mod1}, token);
        manager.Rollback(storage, true, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    [Test]
    public async Task Mod1_Mod2_Patch1()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(Game.REtc, File.Dll, Data.Mod1);
            x.MkFile(Game.DEtc, File.Etc, Data.Mod1);

            x.MkFile(Game.Root, File.Dll, Data.Mod2);
            x.MkFile(Game.Data, File.Etc, Data.Mod2);
            x.MkFile(Game.REtc, File.Exe, Data.Mod2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Mod2);

            x.MkFile(Game.Root, File.Etc, Data.Mod2);
            x.MkFile(Game.Data, File.Exe, Data.Mod2);
            x.MkFile(Game.REtc, File.Vpp, Data.Mod2);
            x.MkFile(Game.DEtc, File.Dll, Data.Mod2);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Mod1);
            x.MkFile(Game.REtc, File.Etc, Data.Mod1);
            x.MkFile(Game.DEtc, File.Exe, Data.Mod1);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // managed
            //x.MkFile(Mngd.Root, File.Vpp, Data.None);
            x.MkFile(Mngd.Data, File.Dll, Data.None);
            x.MkFile(Mngd.REtc, File.Etc, Data.None);
            x.MkFile(Mngd.DEtc, File.Exe, Data.None);

            x.MkFile(Mngd.Root, File.Dll, Data.None);
            x.MkFile(Mngd.Data, File.Etc, Data.None);
            x.MkFile(Mngd.REtc, File.Exe, Data.None);
            x.MkFile(Mngd.DEtc, File.Vpp, Data.None);

            x.MkFile(Mngd.Root, File.Etc, Data.None);
            x.MkFile(Mngd.Data, File.Exe, Data.None);
            x.MkFile(Mngd.REtc, File.Vpp, Data.None);
            x.MkFile(Mngd.DEtc, File.Dll, Data.None);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};
        var pch1 = new Mod {Id = PchId1};


        var result1 = await manager.InstallMod(storage, mod1, false, token);
        var result2 = await manager.InstallMod(storage, mod2, false, token);
        var result3 = await manager.InstallUpdate(storage, new List<IMod> {pch1}, false, new List<IMod>{mod1, mod2}, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
    }

    [Test]
    public async Task Mod1_Mod2_Patch1_Patch2()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(Game.REtc, File.Dll, Data.Mod1);
            x.MkFile(Game.DEtc, File.Etc, Data.Mod1);

            x.MkFile(Game.Root, File.Dll, Data.Mod2);
            x.MkFile(Game.Data, File.Etc, Data.Mod2);
            x.MkFile(Game.REtc, File.Exe, Data.Mod2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Mod2);

            x.MkFile(Game.Root, File.Etc, Data.Mod2);
            x.MkFile(Game.Data, File.Exe, Data.Mod2);
            x.MkFile(Game.REtc, File.Vpp, Data.Mod2);
            x.MkFile(Game.DEtc, File.Dll, Data.Mod2);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Mod1);
            x.MkFile(Game.REtc, File.Etc, Data.Mod1);
            x.MkFile(Game.DEtc, File.Exe, Data.Mod1);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

            // managed
            //x.MkFile(Mngd.Root, File.Vpp, Data.None);
            x.MkFile(Mngd.Data, File.Dll, Data.None);
            x.MkFile(Mngd.REtc, File.Etc, Data.None);
            x.MkFile(Mngd.DEtc, File.Exe, Data.None);

            x.MkFile(Mngd.Root, File.Dll, Data.None);
            x.MkFile(Mngd.Data, File.Etc, Data.None);
            x.MkFile(Mngd.REtc, File.Exe, Data.None);
            x.MkFile(Mngd.DEtc, File.Vpp, Data.None);

            x.MkFile(Mngd.Root, File.Etc, Data.None);
            x.MkFile(Mngd.Data, File.Exe, Data.None);
            x.MkFile(Mngd.REtc, File.Vpp, Data.None);
            x.MkFile(Mngd.DEtc, File.Dll, Data.None);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};


        var result1 = await manager.InstallMod(storage, mod1, false, token);
        var result2 = await manager.InstallMod(storage, mod2, false, token);
        var result3 = await manager.InstallUpdate(storage, new List<IMod> {pch1, pch2}, false, new List<IMod>{mod1, mod2}, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Mod1_Patch2()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(Game.REtc, File.Dll, Data.Mod1);
            x.MkFile(Game.DEtc, File.Etc, Data.Mod1);

            x.MkFile(Game.Root, File.Dll, Data.Pch2);
            x.MkFile(Game.Data, File.Etc, Data.Pch2);
            x.MkFile(Game.REtc, File.Exe, Data.Pch2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(Game.Root, File.Etc, Data.Pch2);
            x.MkFile(Game.Data, File.Exe, Data.Pch2);
            x.MkFile(Game.REtc, File.Vpp, Data.Pch2);
            x.MkFile(Game.DEtc, File.Dll, Data.Pch2);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Mod1);
            x.MkFile(Game.REtc, File.Etc, Data.Mod1);
            x.MkFile(Game.DEtc, File.Exe, Data.Mod1);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

            // managed
            //x.MkFile(Mngd.Root, File.Vpp, Data.None);
            x.MkFile(Mngd.Data, File.Dll, Data.None);
            x.MkFile(Mngd.REtc, File.Etc, Data.None);
            x.MkFile(Mngd.DEtc, File.Exe, Data.None);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};


        var result1 = await manager.InstallUpdate(storage, new List<IMod> {pch1}, false, new List<IMod>{}, token);
        var result2 = await manager.InstallMod(storage, mod1, false, token);
        var result3 = await manager.InstallUpdate(storage, new List<IMod> {pch2}, false, new List<IMod>{mod1}, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Mod1_Patch2_Mod2()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(Game.REtc, File.Dll, Data.Mod1);
            x.MkFile(Game.DEtc, File.Etc, Data.Mod1);

            x.MkFile(Game.Root, File.Dll, Data.Mod2);
            x.MkFile(Game.Data, File.Etc, Data.Mod2);
            x.MkFile(Game.REtc, File.Exe, Data.Mod2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Mod2);

            x.MkFile(Game.Root, File.Etc, Data.Mod2);
            x.MkFile(Game.Data, File.Exe, Data.Mod2);
            x.MkFile(Game.REtc, File.Vpp, Data.Mod2);
            x.MkFile(Game.DEtc, File.Dll, Data.Mod2);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Mod1);
            x.MkFile(Game.REtc, File.Etc, Data.Mod1);
            x.MkFile(Game.DEtc, File.Exe, Data.Mod1);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

            // managed
            //x.MkFile(Mngd.Root, File.Vpp, Data.None);
            x.MkFile(Mngd.Data, File.Dll, Data.None);
            x.MkFile(Mngd.REtc, File.Etc, Data.None);
            x.MkFile(Mngd.DEtc, File.Exe, Data.None);

            x.MkFile(Mngd.Root, File.Dll, Data.None);
            x.MkFile(Mngd.Data, File.Etc, Data.None);
            x.MkFile(Mngd.REtc, File.Exe, Data.None);
            x.MkFile(Mngd.DEtc, File.Vpp, Data.None);

            x.MkFile(Mngd.Root, File.Etc, Data.None);
            x.MkFile(Mngd.Data, File.Exe, Data.None);
            x.MkFile(Mngd.REtc, File.Vpp, Data.None);
            x.MkFile(Mngd.DEtc, File.Dll, Data.None);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};


        var result1 = await manager.InstallUpdate(storage, new List<IMod> {pch1}, false, new List<IMod>{}, token);
        var result2 = await manager.InstallMod(storage, mod1, false, token);
        var result3 = await manager.InstallUpdate(storage, new List<IMod> {pch2}, false, new List<IMod>{mod1}, token);
        var result4 = await manager.InstallMod(storage, mod2, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
        result4.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_Mod1_Mod2_Incremental()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(Game.REtc, File.Dll, Data.Mod1);
            x.MkFile(Game.DEtc, File.Etc, Data.Mod1);

            x.MkFile(Game.Root, File.Dll, Data.Mod2);
            x.MkFile(Game.Data, File.Etc, Data.Mod2);
            x.MkFile(Game.REtc, File.Exe, Data.Mod2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Mod2);

            x.MkFile(Game.Root, File.Etc, Data.Mod2);
            x.MkFile(Game.Data, File.Exe, Data.Mod2);
            x.MkFile(Game.REtc, File.Vpp, Data.Mod2);
            x.MkFile(Game.DEtc, File.Dll, Data.Mod2);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Mod1);
            x.MkFile(Game.REtc, File.Etc, Data.Mod1);
            x.MkFile(Game.DEtc, File.Exe, Data.Mod1);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 1
            x.MkFile(BakP.Root, File.Exe, Data.Pch1);
            x.MkFile(BakP.Data, File.Vpp, Data.Pch1);
            x.MkFile(BakP.REtc, File.Dll, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Etc, Data.Pch1);

            //x.MkFile(BakP.Root, File.Vpp, Data.Pch1);
            x.MkFile(BakP.Data, File.Dll, Data.Pch1);
            x.MkFile(BakP.REtc, File.Etc, Data.Pch1);
            x.MkFile(BakP.DEtc, File.Exe, Data.Pch1);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

            // managed
            //x.MkFile(Mngd.Root, File.Vpp, Data.None);
            x.MkFile(Mngd.Data, File.Dll, Data.None);
            x.MkFile(Mngd.REtc, File.Etc, Data.None);
            x.MkFile(Mngd.DEtc, File.Exe, Data.None);

            x.MkFile(Mngd.Root, File.Dll, Data.None);
            x.MkFile(Mngd.Data, File.Etc, Data.None);
            x.MkFile(Mngd.REtc, File.Exe, Data.None);
            x.MkFile(Mngd.DEtc, File.Vpp, Data.None);

            x.MkFile(Mngd.Root, File.Etc, Data.None);
            x.MkFile(Mngd.Data, File.Exe, Data.None);
            x.MkFile(Mngd.REtc, File.Vpp, Data.None);
            x.MkFile(Mngd.DEtc, File.Dll, Data.None);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};


        var result1 = await manager.InstallUpdate(storage, new List<IMod> {pch1}, false, new List<IMod>{}, token);
        var result2 = await manager.InstallUpdate(storage, new List<IMod> {pch2}, false, new List<IMod>{}, token);
        var result3 = await manager.InstallMod(storage, mod1, false, token);
        var result4 = await manager.InstallMod(storage, mod2, false, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
        result4.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Patch2_FromScratch()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Orig);
            x.MkFile(Game.Data, File.Vpp, Data.Orig);
            x.MkFile(Game.REtc, File.Dll, Data.Orig);
            x.MkFile(Game.DEtc, File.Etc, Data.Orig);

            x.MkFile(Game.Root, File.Dll, Data.Pch2);
            x.MkFile(Game.Data, File.Etc, Data.Pch2);
            x.MkFile(Game.REtc, File.Exe, Data.Pch2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(Game.Root, File.Etc, Data.Pch2);
            x.MkFile(Game.Data, File.Exe, Data.Pch2);
            x.MkFile(Game.REtc, File.Vpp, Data.Pch2);
            x.MkFile(Game.DEtc, File.Dll, Data.Pch2);

            //x.MkFile(Game.Root, File.Vpp, Data.Pch1);
            x.MkFile(Game.Data, File.Dll, Data.Orig).Delete();
            x.MkFile(Game.REtc, File.Etc, Data.Orig).Delete();
            x.MkFile(Game.DEtc, File.Exe, Data.Orig).Delete();

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};


        var result1 = await manager.InstallUpdate(storage, new List<IMod> {pch1}, false, new List<IMod>{}, token);
        var result2 = await manager.InstallUpdate(storage, new List<IMod> {pch2}, true, new List<IMod>{}, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    [Test]
    public async Task Patch1_Mod1_Patch2_FromScratch()
    {
        var fs = Init(x =>
        {
            AddDefaultFiles(x);
        });
        var expected = fs.Clone(x =>
        {
            // modified
            x.MkFile(Game.Root, File.Exe, Data.Mod1);
            x.MkFile(Game.Data, File.Vpp, Data.Mod1);
            x.MkFile(Game.REtc, File.Dll, Data.Mod1);
            x.MkFile(Game.DEtc, File.Etc, Data.Mod1);

            //x.MkFile(Game.Root, File.Vpp, Data.Mod1);
            x.MkFile(Game.Data, File.Dll, Data.Mod1);
            x.MkFile(Game.REtc, File.Etc, Data.Mod1);
            x.MkFile(Game.DEtc, File.Exe, Data.Mod1);

            x.MkFile(Game.Root, File.Dll, Data.Pch2);
            x.MkFile(Game.Data, File.Etc, Data.Pch2);
            x.MkFile(Game.REtc, File.Exe, Data.Pch2);
            x.MkFile(Game.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(Game.Root, File.Etc, Data.Pch2);
            x.MkFile(Game.Data, File.Exe, Data.Pch2);
            x.MkFile(Game.REtc, File.Vpp, Data.Pch2);
            x.MkFile(Game.DEtc, File.Dll, Data.Pch2);

            // bak_vanilla
            x.MkFile(BakV.Root, File.Exe, Data.Orig);
            x.MkFile(BakV.Data, File.Vpp, Data.Orig);
            x.MkFile(BakV.REtc, File.Dll, Data.Orig);
            x.MkFile(BakV.DEtc, File.Etc, Data.Orig);

            // bak_patch 2
            x.MkFile(BakP.Root, File.Dll, Data.Pch2);
            x.MkFile(BakP.Data, File.Etc, Data.Pch2);
            x.MkFile(BakP.REtc, File.Exe, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Vpp, Data.Pch2);

            x.MkFile(BakP.Root, File.Etc, Data.Pch2);
            x.MkFile(BakP.Data, File.Exe, Data.Pch2);
            x.MkFile(BakP.REtc, File.Vpp, Data.Pch2);
            x.MkFile(BakP.DEtc, File.Dll, Data.Pch2);

            // managed
            //x.MkFile(Mngd.Root, File.Vpp, Data.None);
            x.MkFile(Mngd.Data, File.Dll, Data.None);
            x.MkFile(Mngd.REtc, File.Etc, Data.None);
            x.MkFile(Mngd.DEtc, File.Exe, Data.None);
        });
        var hashes = Hashes.StockInAllDirs;
        var storage = new GameStorage(Game.Root, fs, hashes, log);
        var manager = new FileManager(modTools, modInstaller, log);
        var mod1 = new Mod {Id = ModId1};
        var mod2 = new Mod {Id = ModId2};
        var pch1 = new Mod {Id = PchId1};
        var pch2 = new Mod {Id = PchId2};


        var result1 = await manager.InstallUpdate(storage, new List<IMod> {pch1}, false, new List<IMod>{}, token);
        var result2 = await manager.InstallMod(storage, mod1, false, token);
        var result3 = await manager.InstallUpdate(storage, new List<IMod> {pch2}, true, new List<IMod>{mod1}, token);

        fs.ShouldHaveSameFilesAs(expected);
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
    }
}
