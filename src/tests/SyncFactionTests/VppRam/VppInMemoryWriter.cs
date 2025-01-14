using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using SyncFaction.Packer.Models;

namespace SyncFactionTests.VppRam;

public sealed class VppInMemoryWriter : IDisposable
{
    private readonly IList<LogicalInMemoryFile> logicalFiles;
    private readonly RfgVppInMemory.HeaderBlock.Mode mode;
    private readonly LogicalInMemoryArchive logicalInMemoryArchive;

    private readonly byte[] HeaderMagic =
    {
        206,
        10,
        137,
        81
    };

    private readonly byte[] HeaderVersion =
    {
        3,
        0,
        0,
        0
    };

    public VppInMemoryWriter(LogicalInMemoryArchive logicalInMemoryArchive)
    {
        this.logicalInMemoryArchive = logicalInMemoryArchive;
        logicalFiles = logicalInMemoryArchive.LogicalFiles.ToList(); // TODO optimize for streaming?
        mode = logicalInMemoryArchive.Mode;
    }

    public void Dispose() =>
        // GC magic!
        logicalFiles.Clear();

    public async Task WriteAll(Stream s, CancellationToken token)
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

        CheckEntries(token);

        // this is only to get entries block size. offsets and sizes are not computed yet
        var fakeEntriesBlock = await GetEntries(token);
        var entriesPad = RfgVppInMemory.GetPadSize(fakeEntriesBlock.LongLength, 2048, false);

        var entryNamesBlock = await GetEntryNames(token);
        var entryNamesPad = RfgVppInMemory.GetPadSize(entryNamesBlock.LongLength, 2048, false);

        var entriesBlockSize = fakeEntriesBlock.Length + entriesPad;
        var entryNamesBlockSize = entryNamesBlock.Length + entryNamesPad;

        var headerBlockSize = 2048;

        // occupy space for later overwriting
        await WriteZeroes(s, headerBlockSize + entriesBlockSize + entryNamesBlockSize, token);

        var (dataSize, dataCompressedSize) = await WriteDataDetectProfile(s, token);

        var headerBlock = await GetHeader(s.Length, fakeEntriesBlock.Length, entryNamesBlock.Length, dataSize, dataCompressedSize, token);
        var realEntriesBlock = await GetEntries(token);
        s.Position = 0;
        await Write(s, headerBlock, token);
        await Write(s, realEntriesBlock, token);
        await WriteZeroes(s, entriesPad, token);
        await Write(s, entryNamesBlock, token);
        await WriteZeroes(s, entryNamesPad, token);
    }

    public void CheckEntries(CancellationToken token)
    {
        var i = 0;
        foreach (var logicalFile in logicalFiles)
        {
            token.ThrowIfCancellationRequested();
            if (logicalFile.Order != i)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalFile), logicalFile.Order, $"Invalid order, expected {i}");
            }

            if (string.IsNullOrWhiteSpace(logicalFile.Name))
            {
                throw new ArgumentOutOfRangeException(nameof(logicalFile), logicalFile.Name, "Invalid name, expected meaningful string");
            }

            if (string.IsNullOrWhiteSpace(logicalInMemoryArchive.Name))
            {
                throw new ArgumentOutOfRangeException(nameof(logicalFile), logicalInMemoryArchive.Name, "Invalid container name, expected meaningful string");
            }

            i++;
        }
    }

    public async Task<byte[]> GetEntries(CancellationToken token)
    {
        if (logicalFiles.Count == 0)
        {
            return Array.Empty<byte>();
        }

        var currentNameOffset = 0;
        await using var ms = new MemoryStream();
        foreach (var logicalFile in logicalFiles)
        {
            token.ThrowIfCancellationRequested();
            var nameOffset = currentNameOffset;
            var dataOffset = logicalFile.Offset;
            // TODO no idea how to compute compressed size when compacted
            var compressedDataSize = logicalFile.CompressedSize;
            var hash = CircularHash(logicalFile.Name);

            await WriteUint4(ms, nameOffset, token);
            await WriteZeroes(ms, 4, token);
            await WriteUint4(ms, dataOffset, token);
            await Write(ms, hash, token);
            await WriteUint4(ms, logicalFile.Content.Length, token);
            await WriteUint4(ms, compressedDataSize, token);
            await WriteZeroes(ms, 4, token);
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

    public async Task<byte[]> GetEntryNames(CancellationToken token)
    {
        if (logicalFiles.Count == 0)
        {
            return Array.Empty<byte>();
        }

        await using var ms = new MemoryStream();
        foreach (var logicalFile in logicalFiles)
        {
            token.ThrowIfCancellationRequested();
            await Write(ms, logicalFile.NameCString.Value, token);
        }

        return ms.ToArray();
    }

    public async Task<byte[]> GetHeader(long totalSize, int entryBlockLength, int nameBlockLength, uint dataBlockLength, uint compDataBlockLength, CancellationToken token)
    {
        /*
            NOTE: file length is set to 0xFFFFFF for very large archives
        */
        var buffer = new byte[2048];
        await using var ms = new MemoryStream(buffer);
        await Write(ms, HeaderMagic, token);
        await Write(ms, HeaderVersion, token);
        //await WriteString(ms, Title.Value, 65);
        //await WriteString(ms, "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis fermentum, sem tristique finibus ultrices, massa dui facilisis ante, in finibus mauris urna eu justo. Orci varius natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus. Vestibulum ac malesuada enim, ut euismod integer.", 256);
        await WriteString(ms, "", 65, token);
        await WriteString(ms, "", 256, token);
        await WriteZeroes(ms, 3, token);
        await Write(ms,
            new byte[]
            {
                (byte) mode,
                0,
                0,
                0
            },
            token);
        await WriteZeroes(ms, 4, token);
        await WriteUint4(ms, logicalFiles.Count, token);
        await WriteUint4(ms, totalSize, token);
        await WriteUint4(ms, entryBlockLength, token);
        await WriteUint4(ms, nameBlockLength, token);
        await WriteUint4(ms, dataBlockLength, token);
        await WriteUint4(ms, compDataBlockLength, token);
        return buffer;
    }

    private async Task<(uint size, uint compressedSize)> WriteDataDetectProfile(Stream s, CancellationToken token)
    {
        // TODO how to get compDataSize for compressed-only mode?
        /*
            profiles:
            * str2 (both flags, 0)
            * normal vpp (no flags, 2048)
            * compressed vpp (compressed, 2048)
            * compacted vpp (both flags, 16)
        */

        var ext = Path.GetExtension(logicalInMemoryArchive.Name).ToLowerInvariant();
        // all str2 are the same
        if (ext == ".str2_pc")
        {
            // TODO sometimes repacking is not enough and crashes the game. probably need to alter asm_pc file or do magic with offsets inside zlib stream
            return await WriteDataInternal(s, false, true, 9, 0, token);
        }

        // vpp can be different
        return mode switch
        {
            RfgVppInMemory.HeaderBlock.Mode.Normal => await WriteDataInternal(s, false, false, 0, 2048, token),
            RfgVppInMemory.HeaderBlock.Mode.Compressed => await WriteDataInternal(s, true, false, 9, 2048, token),
            RfgVppInMemory.HeaderBlock.Mode.Compacted => await WriteDataInternal(s, false, true, 9, 16, token),
            RfgVppInMemory.HeaderBlock.Mode.Condensed => throw new InvalidOperationException("Condensed-only mode is not present in vanilla files and is not supported")
        };
    }

    private async Task<(uint size, uint compressedSize)> WriteDataInternal(Stream s, bool compressIndividual, bool compressOutput, int compressionLevel, int individualAlignment, CancellationToken token)
    {
        Func<Stream, Stream> wrapperFactory = compressOutput switch
        {
            true => x => new DeflaterOutputStream(x, new Deflater(compressionLevel)) { IsStreamOwner = false },
            false => x => new DisposableStreamWrapper(x)
        };

        uint uncompressedSize = 0;
        uint offset = 0;
        var i = 0;
        var initialPosition = s.Position;
        await using (var output = wrapperFactory(s))
        {
            foreach (var logicalFile in logicalFiles)
            {
                token.ThrowIfCancellationRequested();
                logicalFile.Offset = offset;
                var posBefore = output.Position;
                if (compressIndividual)
                {
                    await CompressZlib(logicalFile.Content, compressionLevel, output, token);
                    logicalFile.CompressedSize = (uint) (output.Position - posBefore);
                    offset += logicalFile.CompressedSize;
                }
                else
                {
                    await Write(output, logicalFile.Content, token);
                    await output.FlushAsync(token);
                    offset += (uint) logicalFile.Content.Length;
                    if (compressOutput)
                    {
                        logicalFile.CompressedSize = (uint) (output.Position - posBefore);
                    }
                }

                uncompressedSize += (uint) logicalFile.Content.Length;
                if (i < logicalFiles.Count - 1)
                {
                    // align if not last entry
                    var padSize = RfgVppInMemory.GetPadSize(offset, individualAlignment, false);
                    await WriteZeroes(output, padSize, token);
                    await output.FlushAsync(token);
                    offset += (uint) padSize;
                    uncompressedSize += (uint) padSize; // TODO is this legit?
                }

                i++;
            }
        }

        var delta = s.Position - initialPosition;
        var compressedSize = compressIndividual || compressOutput
            ? (uint) delta
            : 0xFFFFFFu;
        return (uncompressedSize, compressedSize);
    }

    private async Task Write(Stream stream, byte[] value, CancellationToken token) => await stream.WriteAsync(value, token);

    private async Task WriteZeroes(Stream stream, int count, CancellationToken token)
    {
        if (count == 0)
        {
            return;
        }

        var value = new byte[count];
        await stream.WriteAsync(value, token);
    }

    private async Task WriteUint4(Stream stream, long value, CancellationToken token) => await Write(stream, BitConverter.GetBytes((uint) value), token);

    private async Task WriteUint4(Stream stream, int value, CancellationToken token) => await Write(stream, BitConverter.GetBytes((uint) value), token);

    private async Task WriteString(Stream stream, string value, int targetSize, CancellationToken token)
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

    public static byte[] CircularHash(string input)
    {
        input = input.ToLowerInvariant();

        uint hash = 0;
        for (var i = 0; i < input.Length; i++)
        {
            // rotate left by 6
            hash = (hash << 6) | (hash >> (32 - 6));
            hash = input[i] ^ hash;
        }

        var result = hash;
        return BitConverter.GetBytes(result);
    }

    private static async Task CompressZlib(byte[] data, int compressionLevel, Stream destinationStream, CancellationToken token)
    {
        await using var deflater = new DeflaterOutputStream(destinationStream, new Deflater(compressionLevel)) { IsStreamOwner = false };
        await deflater.WriteAsync(data, token);
    }
}
