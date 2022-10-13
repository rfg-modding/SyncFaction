using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Kaitai;
using SyncFaction.Extras;

namespace SyncFaction.Packer;

public class VppWriter
{
    private readonly IReadOnlyList<LogicalFile> logicalFiles;
    private readonly RfgVpp.HeaderBlock.Mode mode;
    private readonly LogicalArchive logicalArchive;
    private readonly CancellationToken token;

    public VppWriter(LogicalArchive logicalArchive, CancellationToken token)
    {
        this.logicalArchive = logicalArchive;
        this.logicalFiles = logicalArchive.LogicalFiles.ToList(); // TODO optimize for streaming?
        this.mode = logicalArchive.Mode;
        this.token = token;
    }

    public async Task WriteAll(Stream s)
    {
        if (!s.CanSeek)
        {
            throw new ArgumentException($"Need seekable stream, got {s}", nameof(s));
        }

        if (!s.CanWrite)
        {
            throw new ArgumentException($"Need writable stream, got {s}", nameof(s));
        }

        if (s.Position != 0)
        {
            throw new ArgumentException($"Expected start of stream, got position = {s.Position}", nameof(s));
        }


        if (s.Length != 0)
        {
            throw new ArgumentException($"Expected empty stream, got length = {s.Length}", nameof(s));
        }

        CheckEntries();

        // this is only to get entries block size. offsets and sizes are not computed yet
        var fakeEntriesBlock = await GetEntries();
        var entriesPad = Tools.GetPadSize(fakeEntriesBlock.LongLength, 2048, false);

        var entryNamesBlock = await GetEntryNames();
        var entryNamesPad = Tools.GetPadSize(entryNamesBlock.LongLength, 2048, false);

        var entriesBlockSize = fakeEntriesBlock.Length + entriesPad;
        var entryNamesBlockSize = entryNamesBlock.Length + entryNamesPad;

        var headerBlockSize = 2048;

        // occupy space for later overwriting
        await WriteZeroes(s, headerBlockSize + entriesBlockSize + entryNamesBlockSize);

        var (dataSize, dataCompressedSize) = await WriteDataDetectProfile(s);

        var headerBlock = await GetHeader(s.Length, fakeEntriesBlock.Length, entryNamesBlock.Length, dataSize, dataCompressedSize);
        var realEntriesBlock = await GetEntries();
        s.Position = 0;
        await Write(s, headerBlock);
        await Write(s, realEntriesBlock);
        await WriteZeroes(s, entriesPad);
        await Write(s, entryNamesBlock);
        await WriteZeroes(s, entryNamesPad);
    }

    private async Task<(uint size, uint compressedSize)> WriteDataDetectProfile(Stream s)
    {
        // TODO how to get compDataSize for compressed-only mode?
        /*
            profiles:
            * str2 (both flags, 0)
            * normal vpp (no flags, 2048)
            * compressed vpp (compressed, 2048)
            * compacted vpp (both flags, 16)
        */

        var ext = Path.GetExtension(logicalArchive.Name).ToLower();
        // all str2 are the same
        if (ext == ".str2_pc")
        {
            // TODO sometimes repacking is not enough and crashes the game. probably need to alter asm_pc file or do magic with offsets inside zlib stream
            return await WriteDataInternal(s, false, true, 9, 0);
        }
        // vpp can be different
        return mode switch
        {
            RfgVpp.HeaderBlock.Mode.Normal => await WriteDataInternal(s, false, false, 0, 2048),
            RfgVpp.HeaderBlock.Mode.Compressed => await WriteDataInternal(s, true, false, 9, 2048),
            RfgVpp.HeaderBlock.Mode.Compacted => await WriteDataInternal(s, false, true, 9, 16),
            RfgVpp.HeaderBlock.Mode.Condensed => throw new NotImplementedException("Condensed-only mode is not present in vanilla files and is not supported"),
        };
    }

    private async Task<(uint size, uint compressedSize)> WriteDataInternal(Stream s, bool compressIndividual, bool compressOutput, int compressionLevel, int individualAlignment)
    {
        Func<Stream, Stream> wrapperFactory = compressOutput switch
        {
            true => x => new DeflaterOutputStream(x, new Deflater(compressionLevel)){IsStreamOwner = false},
            false => x => new StreamWrapper(x)
        };

        uint uncompressedSize = 0;
        uint offset = 0;
        var i = 0;
        var initialPosition = s.Position;
        await using (var output = wrapperFactory(s))
        {
            foreach (var logicalFile in logicalFiles)
            {
                logicalFile.Offset = offset;
                var posBefore = output.Position;
                if (compressIndividual)
                {

                    await Tools.CompressZlib(logicalFile.Content, compressionLevel, output, token);
                    logicalFile.CompressedSize = (uint)(output.Position - posBefore);
                    offset += logicalFile.CompressedSize;
                }
                else
                {
                    await Write(output, logicalFile.Content);
                    await output.FlushAsync(token);
                    offset += (uint)logicalFile.Content.Length;
                    if (compressOutput)
                    {
                        logicalFile.CompressedSize = (uint)(output.Position - posBefore);
                    }
                }
                uncompressedSize += (uint) logicalFile.Content.Length;
                if (i < logicalFiles.Count - 1)
                {
                    // align if not last entry
                    var padSize = Tools.GetPadSize(offset, individualAlignment, false);
                    await WriteZeroes(output, padSize);
                    await output.FlushAsync(token);
                    offset += (uint) padSize;
                    uncompressedSize += (uint) padSize; // TODO is this legit?
                }
                i++;
            }
        }

        var delta = s.Position - initialPosition;
        var compressedSize = compressIndividual || compressOutput ? (uint)delta : 0xFFFFFFu;
        return (uncompressedSize, compressedSize);
    }


    public void CheckEntries()
    {
        var i = 0;
        foreach (var logicalFile in logicalFiles)
        {
            if (logicalFile.Order != i)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalFile), logicalFile.Order, $"Invalid order, expected {i}");
            }

            if (string.IsNullOrWhiteSpace(logicalFile.Name))
            {
                throw new ArgumentOutOfRangeException(nameof(logicalFile), logicalFile.Name, $"Invalid name, expected meaningful string");
            }

            if (string.IsNullOrWhiteSpace(logicalArchive.Name))
            {
                throw new ArgumentOutOfRangeException(nameof(logicalFile), logicalArchive.Name, $"Invalid container name, expected meaningful string");
            }

            i++;
        }
    }

    public async Task<byte[]> GetEntries()
    {
        if (logicalFiles.Count == 0)
        {
            return Array.Empty<byte>();
        }

        var currentNameOffset = 0;
        await using var ms = new MemoryStream();
        foreach (var logicalFile in logicalFiles)
        {
            var nameOffset = currentNameOffset;
            var dataOffset = logicalFile.Offset;
            // TODO no idea how to compute compressed size when compacted
            var compressedDataSize = logicalFile.CompressedSize;
            var hash = Tools.CircularHash(logicalFile.Name);

            await WriteUint4(ms, nameOffset);
            await WriteZeroes(ms, 4);
            await WriteUint4(ms, dataOffset);
            await Write(ms, hash);
            await WriteUint4(ms, logicalFile.Content.Length);
            await WriteUint4(ms, compressedDataSize);
            await WriteZeroes(ms, 4);
            currentNameOffset += logicalFile.NameCString.Value.Length; // names just go one after another
        }
        return ms.ToArray();
        /*
    - id: name_offset
      type: u4
      doc: Entry name byte offset inside entry names block
    - size: 4
    - id: data_offset
      type: u4
      doc: Entry data byte offset inside entry data block
    - id: name_hash
      size: 4
      doc: Entry name CRC32 hash
    - id: len_data
      type: u4
      doc: Entry data size in bytes
- id: len_compressed_data
type: u4
doc: Compressed entry data size in bytes. If file is not compressed, should be 0xFFFFFFFF
- size: 4
        */
    }

    public async Task<byte[]> GetEntryNames()
    {
        if (logicalFiles.Count == 0)
        {
            return Array.Empty<byte>();
        }

        await using var ms = new MemoryStream();
        foreach (var logicalFile in logicalFiles)
        {
            await Write(ms, logicalFile.NameCString.Value);
        }
        return ms.ToArray();
    }

    public async Task<byte[]> GetHeader(long totalSize, int entryBlockLength, int nameBlockLength, uint dataBlockLength, uint compDataBlockLength)
    {
        /*
            NOTE: file length is set to 0xFFFFFF for very large archives
        */
        var buffer = new byte[2048];
        await using var ms = new MemoryStream(buffer);
        await Write(ms, HeaderMagic);
        await Write(ms, HeaderVersion);
        //await WriteString(ms, Title.Value, 65);
        //await WriteString(ms, "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis fermentum, sem tristique finibus ultrices, massa dui facilisis ante, in finibus mauris urna eu justo. Orci varius natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus. Vestibulum ac malesuada enim, ut euismod integer.", 256);
        await WriteString(ms, "", 65);
        await WriteString(ms, "", 256);
        await WriteZeroes(ms, 3);
        await Write(ms, new byte[] {(byte) mode, 0, 0, 0});
        await WriteZeroes(ms, 4);
        await WriteUint4(ms, logicalFiles.Count);
        await WriteUint4(ms, totalSize);
        await WriteUint4(ms, entryBlockLength);
        await WriteUint4(ms, nameBlockLength);
        await WriteUint4(ms, dataBlockLength);
        await WriteUint4(ms, compDataBlockLength);
        return buffer;
    }

    private async Task Write(Stream stream, byte[] value)
    {
        await stream.WriteAsync(value, token);
    }

    private async Task WriteZeroes(Stream stream, int count)
    {
        if (count == 0)
        {
            return;
        }
        var value = new byte[count];
        await stream.WriteAsync(value, token);
    }

    private async Task WriteUint4(Stream stream, long value)
    {
        await Write(stream, BitConverter.GetBytes((uint) value));
    }

    private async Task WriteUint4(Stream stream, int value)
    {
        await Write(stream, BitConverter.GetBytes((uint) value));
    }

    private async Task WriteString(Stream stream, string value, int targetSize)
    {
        var chars = Encoding.ASCII.GetBytes(value + "\0");
        var fixedSizeChars = GetArrayOfFixedSize(chars, targetSize);
        // string must always end with \0 character!
        fixedSizeChars[targetSize - 1] = 0;
        await stream.WriteAsync(fixedSizeChars, token);
    }

    private byte[] GetArrayOfFixedSize(byte[] source, int targetSize)
    {
        var destination = new byte[targetSize];
        Array.Copy(source, destination, Math.Min(source.Length, destination.Length));
        return destination;
    }

    private byte[] HeaderMagic = new byte[] {206, 10, 137, 81};
    private byte[] HeaderVersion = new byte[] {3, 0, 0, 0};
}
