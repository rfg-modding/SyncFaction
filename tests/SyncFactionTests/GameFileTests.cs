using System.Collections.Immutable;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SyncFaction.Core.Services.Files;

namespace SyncFactionTests;

public class GameFileTests
{
    public MockFileSystem fileSystem;
    public IDirectoryInfo gameDir;
    private Mock<IGameStorage> storageMock;
    private IDirectoryInfo patchBak;
    private IDirectoryInfo bak;
    private IDirectoryInfo managed;
    private IGameStorage Storage => storageMock.Object;


    private static void CreateFile(IFileSystem fileSystem, string absPath)
    {
        var fileInfo = fileSystem.FileInfo.New(absPath);
        fileSystem.Directory.CreateDirectory(fileInfo.Directory.FullName);
        fileInfo.Create().Close();
    }

    private static string ToJson(object o)
    {
        return JsonSerializer.Serialize(o, new JsonSerializerOptions()
        {
            WriteIndented = true
        });
    }

    [SetUp]
    public void Setup()
    {
        fileSystem = new MockFileSystem();
        gameDir = fileSystem.DirectoryInfo.New("x:\\game_dir");
        patchBak = fileSystem.DirectoryInfo.New("x:\\patch_bak");
        bak = fileSystem.DirectoryInfo.New("x:\\bak");
        managed = fileSystem.DirectoryInfo.New("x:\\managed");

        gameDir.Create();
        patchBak.Create();
        bak.Create();
        managed.Create();

        storageMock = new Mock<IGameStorage>();
        storageMock.SetupGet(x => x.Game).Returns(gameDir);
        storageMock.SetupGet(x => x.RootFiles).Returns(new Dictionary<string, string>()
        {
            {"root_file", "root_file"},
            {"rfg", "rfg.exe"},
            {"nonexistent.file", "nonexistent.file"}
        }.ToImmutableSortedDictionary());
        storageMock.SetupGet(x => x.DataFiles).Returns(new Dictionary<string, string>()
        {
            {"data_file", "data/data_file"},
            {"table", "data/table.vpp_pc"},
            {"nonexistent.data", "nonexistent.data"}
        }.ToImmutableSortedDictionary());
        storageMock.SetupGet(x => x.VanillaHashes).Returns(new Dictionary<string, string>()
        {
            {"root_file", "hash of root_file"},
            {"rfg.exe", "hash of rfg.exe"},
            {"data/data_file", "hash of data_file"},
            {"data/table.vpp_pc", "hash of table.vpp_pc"},
            {"nonexistent.file", "hash of nonexistent.file"},
            {"data/nonexistent.data", "hash of nonexistent.data"},
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

        var gf = GameFile.GuessTargetByModFile(Storage, file, modDir);

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
            CreateFile(fileSystem, fileSystem.Path.Combine(patchBak.FullName, modFile));
        }
        if (isManaged)
        {
            CreateFile(fileSystem, fileSystem.Path.Combine(managed.FullName, modFile));
        }

        var gf = GameFile.GuessTargetByModFile(Storage, file, modDir);

        gf.Kind.Should().Be(expected);
    }

    [TestCase("nonexistent.file", null)]
    [TestCase("data/nonexistent.data", null)]
    [TestCase("rfg.exe", "x:\\bak\\rfg.exe")]
    public void CopyToBackup_KnownFile_CleanState(string fileName, string expectedAbsPath)
    {
        // state that matters: IsKnown; Exists; bak exists; patchBak exists
        // also test contents: if stuff is actually copied and owerwritten (or not)
        TODO more tests!
        var gf = new GameFile(Storage, fileName, fileSystem);

        var dst = gf.CopyToBackup(false, false);

        gf.IsKnown.Should().BeTrue();
        dst?.FullName.Should().Be(expectedAbsPath, ToJson(fileSystem.AllPaths));
    }
}
