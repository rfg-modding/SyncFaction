using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SyncFaction.Core.Models.Files;
using SyncFaction.Core.Services.Files;
using SyncFaction.Packer.Services;

namespace SyncFactionTests;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Tests")]
public class GameFileTests
{
    private IGameStorage Storage => storageMock.Object;
    private MockFileSystem fileSystem;
    private IDirectoryInfo gameDir;
    private Mock<IGameStorage> storageMock;
    private IDirectoryInfo patchBak;
    private IDirectoryInfo bak;
    private IDirectoryInfo managed;

    [SetUp]
    public void Setup()
    {
        fileSystem = new MockFileSystem();
        gameDir = fileSystem.DirectoryInfo.New("x:\\game_dir");
        patchBak = fileSystem.DirectoryInfo.New("x:\\bak_patch");
        bak = fileSystem.DirectoryInfo.New("x:\\bak");
        managed = fileSystem.DirectoryInfo.New("x:\\managed");

        gameDir.Create();
        patchBak.Create();
        bak.Create();
        managed.Create();

        storageMock = new Mock<IGameStorage>();
        storageMock.SetupGet(x => x.Game).Returns(gameDir);
        storageMock.SetupGet(x => x.FileSystem).Returns(fileSystem);
        storageMock.SetupGet(x => x.Log).Returns(new NullLogger<GameStorage>());
        storageMock.SetupGet(x => x.RootFiles)
        .Returns(new Dictionary<string, string>
        {
            { "root_file", "root_file" },
            { "rfg", "rfg.exe" },
            { "nonexistent.file", "nonexistent.file" }
        }.ToImmutableSortedDictionary());
        storageMock.SetupGet(x => x.DataFiles)
        .Returns(new Dictionary<string, string>
        {
            { "data_file", "data/data_file" },
            { "table", "data/table.vpp_pc" },
            { "nonexistent.data", "nonexistent.data" }
        }.ToImmutableSortedDictionary());
        storageMock.SetupGet(x => x.VanillaHashes)
        .Returns(new Dictionary<string, string>
        {
            { "root_file", "hash of root_file" },
            { "rfg.exe", "hash of rfg.exe" },
            { "data/data_file", "hash of data_file" },
            { "data/table.vpp_pc", "hash of table.vpp_pc" },
            { "nonexistent.file", "hash of nonexistent.file" },
            { "data/nonexistent.data", "hash of nonexistent.data" }
        }.ToImmutableSortedDictionary());
        storageMock.SetupGet(x => x.PatchBak).Returns(patchBak);
        storageMock.SetupGet(x => x.Bak).Returns(bak);
        storageMock.SetupGet(x => x.Managed).Returns(managed);
    }

    [TestCase("foo")]
    [TestCase("foo\\bar")]
    public void Constructor_SetsAllFields(string path)
    {
        var expected = fileSystem.Path.Join(gameDir.FullName, path);

        var gf = new GameFile(Storage, path, fileSystem);

        gf.AbsolutePath.Should().Be(expected);
        gf.RelativePath.Should().Be(path);
    }

    [TestCase("file", "x:\\game_dir\\file")]
    [TestCase("file.ext", "x:\\game_dir\\file.ext")]
    [TestCase("folder\\file", "x:\\game_dir\\folder\\file")]
    [TestCase("folder\\file.ext", "x:\\game_dir\\folder\\file.ext")]
    [TestCase("root_file", "x:\\game_dir\\root_file")]
    [TestCase("root_file.xdelta", "x:\\game_dir\\root_file")]
    [TestCase("rfg", "x:\\game_dir\\rfg.exe")]
    [TestCase("rfg.exe", "x:\\game_dir\\rfg.exe")]
    [TestCase("rfg.xdelta", "x:\\game_dir\\rfg.exe")]
    [TestCase("data_file", "x:\\game_dir\\data\\data_file")]
    [TestCase("table", "x:\\game_dir\\data\\table.vpp_pc")]
    [TestCase("table.vpp_pc", "x:\\game_dir\\data\\table.vpp_pc")]
    [TestCase("table.xdelta", "x:\\game_dir\\data\\table.vpp_pc")]
    [TestCase("data\\data_file", "x:\\game_dir\\data\\data_file")]
    [TestCase("data\\data_file.xdelta", "x:\\game_dir\\data\\data_file")]
    [TestCase("data\\table", "x:\\game_dir\\data\\table.vpp_pc")]
    [TestCase("data\\table.vpp_pc", "x:\\game_dir\\data\\table.vpp_pc")]
    [TestCase("data\\table.xdelta", "x:\\game_dir\\data\\table.vpp_pc")]
    [TestCase("data\\rfg.exe", "x:\\game_dir\\data\\rfg.exe")]
    public void GuessTargetByModFile_Works(string modFile, string expected)
    {
        var modDir = fileSystem.DirectoryInfo.New("x:\\mod");
        var file = fileSystem.FileInfo.New(fileSystem.Path.Join(modDir.FullName, modFile));

        var gf = GameFile.GuessTarget(Storage, file, modDir, new NullLogger<GameFile>());

        gf.AbsolutePath.Should().Be(expected);
    }

    [TestCase("rfg.exe", false, false, FileKind.Stock)]
    [TestCase("data/table.vpp_pc", false, false, FileKind.Stock)]
    [TestCase("rfg.exe", true, false, FileKind.Stock)]
    [TestCase("rfg.exe", false, true, FileKind.Stock)]
    [TestCase("data/table.vpp_pc", true, false, FileKind.Stock)]
    [TestCase("data/table.vpp_pc", false, true, FileKind.Stock)]
    [TestCase("rfg.exe", true, true, FileKind.Stock)]
    [TestCase("data/table.vpp_pc", true, true, FileKind.Stock)]
    [TestCase("new_file", false, false, FileKind.Unmanaged)]
    [TestCase("new_file", true, false, FileKind.FromPatch)]
    [TestCase("new_file", false, true, FileKind.FromMod)]
    [TestCase("data/new_file", true, false, FileKind.FromPatch)]
    [TestCase("data/new_file", false, true, FileKind.FromMod)]
    [TestCase("data/new_file", false, false, FileKind.Unmanaged)]
    public void Kind_Works(string modFile, bool hasPatchBak, bool isManaged, FileKind expected)
    {
        var modDir = fileSystem.DirectoryInfo.New("x:\\mod");
        var file = fileSystem.FileInfo.New(fileSystem.Path.Join(modDir.FullName, modFile));
        if (hasPatchBak)
        {
            Create(fileSystem.Path.Combine(patchBak.FullName, modFile));
        }

        if (isManaged)
        {
            Create(fileSystem.Path.Combine(managed.FullName, modFile));
        }

        var gf = GameFile.GuessTarget(Storage, file, modDir, new NullLogger<GameFile>());

        gf.Kind.Should().Be(expected);
    }

    [TestCase("nonexistent.file", null)]
    [TestCase("data/nonexistent.data", null)]
    [TestCase("rfg.exe", "x:\\bak\\rfg.exe")]
    [TestCase("data/table.vpp_pc", "x:\\bak\\data\\table.vpp_pc")]
    public void CopyToBackup_KnownFileOnCleanState_CopiesToVanillaOrNowhere(string fileName, string expectedAbsPath)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);

        var dst = gf.CopyToBackup(false, false);

        gf.IsKnown.Should().BeTrue();
        dst?.FullName.Should().Be(expectedAbsPath, ToJson(fileSystem.AllPaths));
    }

    // state that matters: IsKnown; Exists; bak exists; patchBak exists
    // TODO also test contents: if stuff is actually copied and owerwritten (or not)

    [TestCase("rfg.exe", "x:\\bak\\rfg.exe")]
    [TestCase("data/table.vpp_pc", "x:\\bak\\data\\table.vpp_pc")]
    public void CopyToBackup_KnownFileAlreadyInVanillaOnOverwrite_Throws(string fileName, string expectedAbsPath)
    {
        var data1 = "test1";
        var data2 = "test2";
        var gf = new GameFile(Storage, fileName, fileSystem);
        Create(gf.AbsolutePath);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data1);
        }

        var _ = gf.CopyToBackup(false, false);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data2);
        }

        var action = () => gf.CopyToBackup(true, false);

        action.Should().Throw<InvalidOperationException>();
    }

    [TestCase("rfg.exe", "x:\\bak\\rfg.exe")]
    [TestCase("data/table.vpp_pc", "x:\\bak\\data\\table.vpp_pc")]
    public void CopyToBackup_KnownFileAlreadyInVanillaDontOverwrite_DoesNothing(string fileName, string expectedAbsPath)
    {
        var data1 = "test1";
        var data2 = "test2";
        var gf = new GameFile(Storage, fileName, fileSystem);
        Create(gf.AbsolutePath);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data1);
        }

        var _ = gf.CopyToBackup(false, false);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data2);
        }

        var dst = gf.CopyToBackup(false, false);

        dst?.FullName.Should().Be(expectedAbsPath, ToJson(fileSystem.AllPaths));
        using (var s = dst.OpenRead())
        {
            using var x = new StreamReader(s);
            x.ReadToEnd().Should().Be(data1);
        }
    }

    [TestCase("rfg.exe", "x:\\bak_patch\\rfg.exe")]
    [TestCase("data/table.vpp_pc", "x:\\bak_patch\\data\\table.vpp_pc")]
    public void CopyToBackup_KnownFileOnUpdateWhenVanillaExists_CopiesToPatch(string fileName, string expectedAbsPath)
    {
        var data1 = "test1";
        var data2 = "test2";
        var gf = new GameFile(Storage, fileName, fileSystem);
        Create(gf.AbsolutePath);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data1);
        }

        var _ = gf.CopyToBackup(false, false);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data2);
        }

        var dst = gf.CopyToBackup(true, true);

        dst?.FullName.Should().Be(expectedAbsPath, ToJson(fileSystem.AllPaths));
        using (var s = dst.OpenRead())
        {
            using var x = new StreamReader(s);
            x.ReadToEnd().Should().Be(data2);
        }
    }

    [TestCase("rfg.exe", "x:\\bak\\rfg.exe")]
    [TestCase("data/table.vpp_pc", "x:\\bak\\data\\table.vpp_pc")]
    public void CopyToBackup_KnownFileOnUpdateNoBackupExists_CopiesToVanilla(string fileName, string expectedAbsPath)
    {
        var data1 = "test1";
        var gf = new GameFile(Storage, fileName, fileSystem);
        Create(gf.AbsolutePath);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data1);
        }

        var dst = gf.CopyToBackup(true, true);

        dst?.FullName.Should().Be(expectedAbsPath, ToJson(fileSystem.AllPaths));
        using (var s = dst.OpenRead())
        {
            using var x = new StreamReader(s);
            x.ReadToEnd().Should().Be(data1);
        }
    }

    [TestCase("rfg.exe", "x:\\bak_patch\\rfg.exe")]
    [TestCase("data/table.vpp_pc", "x:\\bak_patch\\data\\table.vpp_pc")]
    public void CopyToBackup_KnownFileOnUpdateBothExist_OverwritesPatch(string fileName, string expectedAbsPath)
    {
        var data1 = "test1";
        var data2 = "test2";
        var gf = new GameFile(Storage, fileName, fileSystem);
        Create(gf.AbsolutePath);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data1);
        }

        gf.CopyToBackup(false, false);
        gf.CopyToBackup(true, true);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data2);
        }

        var dst = gf.CopyToBackup(true, true);

        dst?.FullName.Should().Be(expectedAbsPath, ToJson(fileSystem.AllPaths));
        using (var s = dst.OpenRead())
        {
            using var x = new StreamReader(s);
            x.ReadToEnd().Should().Be(data2);
        }
    }

    [TestCase("new_file", "x:\\bak_patch\\new_file")]
    [TestCase("data/new_file", "x:\\bak_patch\\data\\new_file")]
    public void CopyToBackup_NewFilePatchExistsDontOverwriteOnUpdate_DoesNothing(string fileName, string expectedAbsPath)
    {
        var data1 = "test1";
        var data2 = "test2";
        var gf = new GameFile(Storage, fileName, fileSystem);
        Create(gf.AbsolutePath);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data1);
        }

        var _ = gf.CopyToBackup(false, true);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data2);
        }

        var dst = gf.CopyToBackup(false, true);

        dst?.FullName.Should().Be(expectedAbsPath, ToJson(fileSystem.AllPaths));
        using (var s = dst.OpenRead())
        {
            using var x = new StreamReader(s);
            x.ReadToEnd().Should().Be(data1);
        }
    }

    [TestCase("new_file", "x:\\bak_patch\\new_file")]
    [TestCase("data/new_file", "x:\\bak_patch\\data\\new_file")]
    public void CopyToBackup_NewFileOnUpdateNoBackupExists_CopiesToPatch(string fileName, string expectedAbsPath)
    {
        var data1 = "test1";
        var gf = new GameFile(Storage, fileName, fileSystem);
        Create(gf.AbsolutePath);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data1);
        }

        var dst = gf.CopyToBackup(true, true);

        dst?.FullName.Should().Be(expectedAbsPath, ToJson(fileSystem.AllPaths));
        using (var s = dst.OpenRead())
        {
            using var x = new StreamReader(s);
            x.ReadToEnd().Should().Be(data1);
        }
    }

    [TestCase("new_file", "x:\\bak_patch\\new_file")]
    [TestCase("data/new_file", "x:\\bak_patch\\data\\new_file")]
    public void CopyToBackup_NewFileOnUpdateWhenPatchExists_OverwritesPatch(string fileName, string expectedAbsPath)
    {
        var data1 = "test1";
        var data2 = "test2";
        var gf = new GameFile(Storage, fileName, fileSystem);
        Create(gf.AbsolutePath);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data1);
        }

        gf.CopyToBackup(false, false);
        gf.CopyToBackup(true, true);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data2);
        }

        var dst = gf.CopyToBackup(true, true);

        dst?.FullName.Should().Be(expectedAbsPath, ToJson(fileSystem.AllPaths));
        using (var s = dst.OpenRead())
        {
            using var x = new StreamReader(s);
            x.ReadToEnd().Should().Be(data2);
        }
    }

    [TestCase("new_file", "x:\\managed\\new_file")]
    [TestCase("data/new_file", "x:\\managed\\data\\new_file")]
    public void CopyToBackup_NewFileNotUpdate_TracksAsManaged(string fileName, string expectedAbsPath)
    {
        var data1 = "test1";
        var gf = new GameFile(Storage, fileName, fileSystem);
        Create(gf.AbsolutePath);
        using (var s = gf.FileInfo.CreateText())
        {
            s.Write(data1);
        }

        var dst = gf.CopyToBackup(true, false);

        dst?.FullName.Should().Be(expectedAbsPath, ToJson(fileSystem.AllPaths));
        using (var s = dst.OpenRead())
        {
            using var x = new StreamReader(s);
            x.ReadToEnd().Should().BeEmpty();
        }
    }

    [TestCase(false, false, true)]
    [TestCase(false, true, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public void FindBackup_Works(bool vanillaExists, bool patchExists, bool vanillaExpected)
    {
        var gf = new GameFile(Storage, "test", fileSystem);
        if (vanillaExists)
        {
            fileSystem.FileInfo.New(fileSystem.Path.Combine(bak.FullName, "test")).Create();
        }

        if (patchExists)
        {
            fileSystem.FileInfo.New(fileSystem.Path.Combine(patchBak.FullName, "test")).Create();
        }

        var result = gf.FindBackup();
        var resultDir = result.Directory.FullName;

        var expected = vanillaExpected
            ? bak.FullName
            : patchBak.FullName;
        resultDir.Should().Be(expected);
    }

    [TestCase("rfg.exe", true, true, VanillaData)]
    [TestCase("rfg.exe", true, false, VanillaData)]
    [TestCase("data/table.vpp_pc", true, true, VanillaData)]
    [TestCase("data/table.vpp_pc", true, false, VanillaData)]
    public void RollbackToVanilla_KnownFileHasVanillaBackup_Overwrites(string fileName, bool vanillaExists, bool patchExists, string expected)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        Write(gf.AbsolutePath, DirtyData);

        if (vanillaExists)
        {
            Write(gf.GetVanillaBackupLocation().FullName, VanillaData);
        }

        if (patchExists)
        {
            Write(gf.GetPatchBackupLocation().FullName, PatchData);
        }

        gf.Rollback(true);

        Read(gf.FileInfo).Should().Be(expected);
    }

    /// <summary>
    /// file not present in backups == it was never modified, this is not an error
    /// </summary>
    [TestCase("rfg.exe", false, true, DirtyData)]
    [TestCase("rfg.exe", false, false, DirtyData)]
    [TestCase("data/table.vpp_pc", false, true, DirtyData)]
    [TestCase("data/table.vpp_pc", false, false, DirtyData)]
    public void RollbackToVanilla_KnownFileNoVanillaBackup_KeepsExisting(string fileName, bool vanillaExists, bool patchExists, string expected)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        Write(gf.AbsolutePath, DirtyData);

        if (vanillaExists)
        {
            Write(gf.GetVanillaBackupLocation().FullName, VanillaData);
        }

        if (patchExists)
        {
            Write(gf.GetPatchBackupLocation().FullName, PatchData);
        }

        gf.Rollback(true);

        Read(gf.FileInfo).Should().Be(expected);
    }

    [TestCase("rfg.exe", true, true, PatchData)]
    [TestCase("rfg.exe", false, true, PatchData)]
    [TestCase("data/table.vpp_pc", true, true, PatchData)]
    [TestCase("data/table.vpp_pc", false, true, PatchData)]
    public void RollbackToPatch_KnownFileHasPatchBackup_Overwrites(string fileName, bool vanillaExists, bool patchExists, string expected)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        Write(gf.AbsolutePath, DirtyData);

        if (vanillaExists)
        {
            Write(gf.GetVanillaBackupLocation().FullName, VanillaData);
        }

        if (patchExists)
        {
            Write(gf.GetPatchBackupLocation().FullName, PatchData);
        }

        gf.Rollback(false);

        Read(gf.FileInfo).Should().Be(expected);
    }

    /// <summary>
    /// file not present in backups == it was never modified, this is not an error
    /// </summary>
    [TestCase("rfg.exe", true, false, VanillaData)]
    [TestCase("rfg.exe", false, false, DirtyData)]
    [TestCase("data/table.vpp_pc", true, false, VanillaData)]
    [TestCase("data/table.vpp_pc", false, false, DirtyData)]
    public void RollbackToPatch_KnownFileNoPatchBackup_RestoresFromVanilla(string fileName, bool vanillaExists, bool patchExists, string expected)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        Write(gf.AbsolutePath, DirtyData);

        if (vanillaExists)
        {
            Write(gf.GetVanillaBackupLocation().FullName, VanillaData);
        }

        if (patchExists)
        {
            Write(gf.GetPatchBackupLocation().FullName, PatchData);
        }

        gf.Rollback(false);

        Read(gf.FileInfo).Should().Be(expected);
    }

    [TestCase("new_file")]
    [TestCase("data/new_file")]
    public void RollbackToVanilla_PatchFile_Deletes(string fileName)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        Write(gf.AbsolutePath, DirtyData);
        Write(gf.GetPatchBackupLocation().FullName, PatchData);

        gf.Rollback(true);

        gf.Exists.Should().BeFalse();
    }

    [TestCase("new_file")]
    [TestCase("data/new_file")]
    public void RollbackToPatch_PatchFile_Overwrites(string fileName)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        Write(gf.AbsolutePath, DirtyData);
        Write(gf.GetPatchBackupLocation().FullName, PatchData);

        gf.Rollback(false);

        Read(gf.FileInfo).Should().Be(PatchData);
    }

    [TestCase("new_file", true)]
    [TestCase("new_file", false)]
    [TestCase("data/new_file", true)]
    [TestCase("data/new_file", false)]
    public void Rollback_ManagedFile_Deletes(string fileName, bool vanilla)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        var managed = gf.GetManagedLocation();
        Write(gf.AbsolutePath, DirtyData);
        Create(managed.FullName);
        gf.FileInfo.Refresh();
        managed.Refresh();

        gf.Rollback(vanilla);

        gf.FileInfo.Refresh();
        managed.Refresh();

        gf.Exists.Should().BeFalse();
        managed.Exists.Should().BeFalse();
    }

    [TestCase("new_file", true)]
    [TestCase("new_file", false)]
    [TestCase("data/new_file", true)]
    [TestCase("data/new_file", false)]
    public void Rollback_ManagedFileAlreadyDeleted_Forgets(string fileName, bool vanilla)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        var managed = gf.GetManagedLocation();
        Write(gf.AbsolutePath, DirtyData);
        Create(managed.FullName);
        gf.FileInfo.Refresh();
        managed.Refresh();
        gf.Delete();
        gf.FileInfo.Refresh();

        gf.Rollback(vanilla);

        gf.FileInfo.Refresh();
        managed.Refresh();

        gf.Exists.Should().BeFalse();
        managed.Exists.Should().BeFalse();
    }

    [TestCase("new_file", true)]
    [TestCase("new_file", false)]
    [TestCase("data/new_file", true)]
    [TestCase("data/new_file", false)]
    public void Rollback_UnmanagedFile_Throws(string fileName, bool vanilla)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        Write(gf.AbsolutePath, DirtyData);

        var action = () => gf.Rollback(vanilla);

        action.Should().Throw<InvalidOperationException>();
    }

    [TestCase("file", ".xdelta")]
    [TestCase("file", ".Xdelta")]
    [TestCase("file", ".xDelta")]
    [TestCase("file", ".XDelta")]
    [TestCase("file", ".XDELTA")]
    [TestCase("file.vpp_pc", ".xdelta")]
    [TestCase("file.vpp_pc", ".Xdelta")]
    [TestCase("file.vpp_pc", ".xDelta")]
    [TestCase("file.vpp_pc", ".XDelta")]
    [TestCase("file.vpp_pc", ".XDELTA")]
    public async Task ApplyMod_Xdelta_CallsApplyXdelta(string fileName, string modExt)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        var modInstallerMock = new Mock<ModInstaller>(Mock.Of<IVppArchiver>(), new FakeXdeltaFactory(), null, new NullLogger<ModInstaller>()) { CallBase = true };
        modInstallerMock.Setup(x => x.ApplyXdelta(gf, It.IsAny<IFileInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(true).Verifiable();

        var modFile = fileSystem.FileInfo.New(fileName + modExt);

        await modInstallerMock.Object.ApplyFileMod(gf, modFile, CancellationToken.None);

        modInstallerMock.Verify();
    }

    [TestCase("file", ".rfgpatch")]
    [TestCase("file", ".Rfgpatch")]
    [TestCase("file", ".rfgPatch")]
    [TestCase("file", ".RfgPatch")]
    [TestCase("file", ".RFGPATCH")]
    [TestCase("file.vpp_pc", ".rfgpatch")]
    [TestCase("file.vpp_pc", ".Rfgpatch")]
    [TestCase("file.vpp_pc", ".rfgPatch")]
    [TestCase("file.vpp_pc", ".RfgPatch")]
    [TestCase("file.vpp_pc", ".RFGPATCH")]
    public async Task ApplyMod_RfgPatch_CallsSkip(string fileName, string modExt)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        var modInstallerMock = new Mock<ModInstaller>(Mock.Of<IVppArchiver>(), new FakeXdeltaFactory(), null, new NullLogger<ModInstaller>()) { CallBase = true };
        modInstallerMock.Setup(x => x.Skip(gf, It.IsAny<IFileInfo>())).Returns(true).Verifiable();

        var modFile = fileSystem.FileInfo.New(fileName + modExt);

        await modInstallerMock.Object.ApplyFileMod(gf, modFile, CancellationToken.None);

        modInstallerMock.Verify();
    }

    [TestCase("file", ".xdelta")]
    [TestCase("file.vpp_pc", ".xdelta")]
    public async Task ApplyMod_DotModFile_CallsSkip(string fileName, string modExt)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        var modInstallerMock = new Mock<ModInstaller>(Mock.Of<IVppArchiver>(), new FakeXdeltaFactory(), null, new NullLogger<ModInstaller>()) { CallBase = true };
        modInstallerMock.Setup(x => x.Skip(gf, It.IsAny<IFileInfo>())).Returns(true).Verifiable();

        var modFile = fileSystem.FileInfo.New(".mod_" + fileName + modExt);

        await modInstallerMock.Object.ApplyFileMod(gf, modFile, CancellationToken.None);

        modInstallerMock.Verify();
    }

    [TestCase("file", ".txt")]
    [TestCase("file", ".JPG")]
    [TestCase("file", ".Jpeg")]
    [TestCase("file", ".png")]
    [TestCase("file", ".7Z")]
    [TestCase("file", ".zip")]
    [TestCase("file", ".RAR")]
    [TestCase("file.vpp_pc", ".txt")]
    [TestCase("file.vpp_pc", ".JPG")]
    [TestCase("file.vpp_pc", ".Jpeg")]
    [TestCase("file.vpp_pc", ".png")]
    [TestCase("file.vpp_pc", ".7Z")]
    [TestCase("file.vpp_pc", ".zip")]
    [TestCase("file.vpp_pc", ".RAR")]
    public async Task ApplyMod_Clutter_CallsSkip(string fileName, string modExt)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        var modInstallerMock = new Mock<ModInstaller>(Mock.Of<IVppArchiver>(), new FakeXdeltaFactory(), null, new NullLogger<ModInstaller>()) { CallBase = true };
        modInstallerMock.Setup(x => x.Skip(gf, It.IsAny<IFileInfo>())).Returns(true).Verifiable();

        var modFile = fileSystem.FileInfo.New(fileName + modExt);

        await modInstallerMock.Object.ApplyFileMod(gf, modFile, CancellationToken.None);

        modInstallerMock.Verify();
    }

    [TestCase("file.bin")]
    [TestCase("file.EXE")]
    [TestCase("file.foo.bar")]
    public async Task ApplyMod_Xdelta_CallsApplyNewFile(string fileName)
    {
        var gf = new GameFile(Storage, fileName, fileSystem);
        var modInstallerMock = new Mock<ModInstaller>(Mock.Of<IVppArchiver>(), new FakeXdeltaFactory(), null, new NullLogger<ModInstaller>()) { CallBase = true };
        modInstallerMock.Setup(x => x.ApplyNewFile(gf, It.IsAny<IFileInfo>())).Returns(true).Verifiable();

        var modFile = fileSystem.FileInfo.New(fileName);

        await modInstallerMock.Object.ApplyFileMod(gf, modFile, CancellationToken.None);

        modInstallerMock.Verify();
    }

    [Test]
    public void Skip_ReturnsTrue()
    {
        var gf = new GameFile(Storage, "test", fileSystem);
        var modInstallerMock = new Mock<ModInstaller>(Mock.Of<IVppArchiver>(), new FakeXdeltaFactory(), null, new NullLogger<ModInstaller>()) { CallBase = true };
        modInstallerMock.Object.Skip(gf, Mock.Of<IFileInfo>()).Should().BeTrue();
    }

    [Test]
    public async Task ApplyNewFile_FileExists_Overwrites()
    {
        var gf = new GameFile(Storage, "test", fileSystem);
        var modInstallerMock = new Mock<ModInstaller>(Mock.Of<IVppArchiver>(), new FakeXdeltaFactory(), null, new NullLogger<ModInstaller>()) { CallBase = true };
        var modFile = fileSystem.FileInfo.New("mod");
        Write(gf.AbsolutePath, DirtyData);
        Write(modFile.FullName, ModData);
        modInstallerMock.Object.ApplyNewFile(gf, modFile);
        Read(gf.FileInfo).Should().Be(ModData);
    }

    [Test]
    public async Task ApplyNewFile_FileDoesNotExist_Copies()
    {
        var gf = new GameFile(Storage, "data/test", fileSystem);
        var modInstallerMock = new Mock<ModInstaller>(Mock.Of<IVppArchiver>(), new FakeXdeltaFactory(), null, new NullLogger<ModInstaller>()) { CallBase = true };
        var modFile = fileSystem.FileInfo.New("mod");
        Write(modFile.FullName, ModData);
        modInstallerMock.Object.ApplyNewFile(gf, modFile);
        Read(gf.FileInfo).Should().Be(ModData);
    }

    [Test]
    public async Task ApplyXdelta_VanillaBakExists_UsesCurrentFile()
    {
        var gf = new GameFile(Storage, "test", fileSystem);
        var fakeXdeltaFactory = new FakeXdeltaFactory();
        var modInstaller = new ModInstaller(Mock.Of<IVppArchiver>(), fakeXdeltaFactory, null, new NullLogger<ModInstaller>());

        var modFile = fileSystem.FileInfo.New("mod");
        Write(gf.AbsolutePath, DirtyData);
        gf.FileInfo.Refresh();
        Write(gf.GetVanillaBackupLocation().FullName, VanillaData);
        Write(modFile.FullName, ModData);

        await modInstaller.ApplyXdelta(gf, modFile, CancellationToken.None);
        Read(gf.FileInfo).Should().Be(DirtyData + ModData);
    }

    [Test]
    public async Task ApplyXdelta_PatchBakExists_UsesCurrentFile()
    {
        var gf = new GameFile(Storage, "test", fileSystem);
        var fakeXdeltaFactory = new FakeXdeltaFactory();
        var modInstaller = new ModInstaller(Mock.Of<IVppArchiver>(), fakeXdeltaFactory, null, new NullLogger<ModInstaller>());

        var modFile = fileSystem.FileInfo.New("mod");
        Write(gf.AbsolutePath, DirtyData);
        gf.FileInfo.Refresh();
        Write(gf.GetPatchBackupLocation().FullName, PatchData);
        Write(modFile.FullName, ModData);

        await modInstaller.ApplyXdelta(gf, modFile, CancellationToken.None);
        Read(gf.FileInfo).Should().Be(DirtyData + ModData);
    }

    [Test]
    public async Task ApplyXdelta_BothBakExist_UsesCurrentFile()
    {
        var gf = new GameFile(Storage, "test", fileSystem);
        var fakeXdeltaFactory = new FakeXdeltaFactory();
        var modInstaller = new ModInstaller(Mock.Of<IVppArchiver>(), fakeXdeltaFactory, null, new NullLogger<ModInstaller>());

        var modFile = fileSystem.FileInfo.New("mod");
        Write(gf.AbsolutePath, DirtyData);
        gf.FileInfo.Refresh();
        Write(gf.GetVanillaBackupLocation().FullName, VanillaData);
        Write(gf.GetPatchBackupLocation().FullName, PatchData);
        Write(modFile.FullName, ModData);

        await modInstaller.ApplyXdelta(gf, modFile, CancellationToken.None);
        Read(gf.FileInfo).Should().Be(DirtyData + ModData);
    }

    [Test]
    public async Task ApplyXdelta_NoFileExists_Throws()
    {
        FakeXdeltaConcat? fakeXdelta = null;
        var gf = new GameFile(Storage, "test", fileSystem);
        var modInstaller = new ModInstaller(Mock.Of<IVppArchiver>(), new FakeXdeltaFactory(), null, new NullLogger<ModInstaller>());

        var modFile = fileSystem.FileInfo.New("mod");
        Write(modFile.FullName, ModData);

        Func<Task> action = async () => await modInstaller.ApplyXdelta(gf, modFile, CancellationToken.None);

        await action.Should().ThrowAsync<FileNotFoundException>();
    }

    [Test]
    public async Task ApplyXdelta_Canceled_Throws()
    {
        FakeXdeltaConcat? fakeXdelta = null;
        var gf = new GameFile(Storage, "test", fileSystem);
        var modInstaller = new ModInstaller(Mock.Of<IVppArchiver>(), new FakeXdeltaFactory(), null, new NullLogger<ModInstaller>());

        var modFile = fileSystem.FileInfo.New("mod");
        Write(gf.AbsolutePath, DirtyData);
        gf.FileInfo.Refresh();
        Write(gf.GetVanillaBackupLocation().FullName, VanillaData);
        Write(modFile.FullName, ModData);

        Func<Task> action = async () => await modInstaller.ApplyXdelta(gf, modFile, new CancellationToken(true));

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Creates empty file with directories if needed
    /// </summary>
    private void Create(string absPath)
    {
        var fileInfo = fileSystem.FileInfo.New(absPath);
        fileSystem.Directory.CreateDirectory(fileInfo.Directory.FullName);
        fileInfo.Create().Close();
    }

    /// <summary>
    /// Writes to a new file, creating directories if needed
    /// </summary>
    private void Write(string absPath, string content)
    {
        Create(absPath);
        var fileInfo = fileSystem.FileInfo.New(absPath);
        using var s = fileInfo.CreateText();
        s.Write(content);
    }

    private string Read(IFileInfo fileInfo)
    {
        fileInfo.Refresh();
        using var s = fileInfo.OpenRead();
        using var x = new StreamReader(s);
        return x.ReadToEnd();
    }

    private static string ToJson(object o) =>
        JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true });

    public const string DirtyData = "dirty";
    public const string ModData = "mod";
    public const string VanillaData = "vanilla";
    public const string PatchData = "patch";
}
