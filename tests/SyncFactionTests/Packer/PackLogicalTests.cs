using FluentAssertions;
using SyncFaction.Packer;

namespace SyncFactionTests.Packer;

[Explicit("Depend on paths tied to steam version")]
public class PackLogicalTests
{
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllFiles))]
    public async Task Test(FileInfo fileInfo)
    {
        if (fileInfo.Extension != ".vpp_pc" || fileInfo.Extension != ".str2_pc")
        {
            Assert.Ignore("Not an archive");
        }
        using var fileStream = fileInfo.OpenRead();
        var archive = Tools.UnpackVpp(fileStream, fileInfo.Name);

        var dstFile = new FileInfo(Path.Combine(TestUtils.ArtifactDir.FullName, fileInfo.Name + ".repacked"));
        await using (var dstStream = dstFile.OpenWrite())
        {
            var writer = new VppWriter(archive, CancellationToken.None);
            await writer.WriteAll(dstStream);
        }

        dstFile.Refresh();
        if (dstFile.Length != fileInfo.Length)
        {
            Assert.Warn($"Result length {dstFile.Length} != source length {fileInfo.Length}. Difference {dstFile.Length - fileInfo.Length}");
        }

        await using var s = dstFile.OpenRead();
        var repack = Tools.UnpackVpp(s, dstFile.Name);
        repack.Mode.Should().Be(archive.Mode);
        var i = 0;
        fileStream.Position = 0;
        var src2 = Tools.UnpackVpp(fileStream, fileInfo.Name);
        var srcFiles = src2.LogicalFiles.ToList();
        foreach (var repackLogicalFile in repack.LogicalFiles)
        {
            var srcFile = srcFiles[i];
            var description = $"failed at index {i}\nrepack: size={repackLogicalFile.Content.Length}\n{repackLogicalFile}\n\nsrc: size={srcFile.Content.Length}\n{srcFile}";
            repackLogicalFile.Name.Should().Be(srcFile.Name, description);
            repackLogicalFile.Order.Should().Be(srcFile.Order, description);
            repackLogicalFile.Content.Length.Should().Be(srcFile.Content.Length, description);
            //repackLogicalFile.Content.Should().BeEquivalentTo(srcFile.Content);
            //Assert.That(repackLogicalFile.Content, Is.EqualTo(srcFile.Content));
            var repackHash = TestUtils.ComputeHash(new MemoryStream(repackLogicalFile.Content));
            var hash = TestUtils.ComputeHash(new MemoryStream(srcFile.Content));
            repackHash.Should().Be(hash, description);
            i++;
        }
    }

    [OneTimeSetUp]
    [TearDown]
    public void DeleteFiles()
    {
        foreach (var file in TestUtils.ArtifactDir.EnumerateFiles("*.repacked"))
        {
            file.Delete();
        }
    }
}
