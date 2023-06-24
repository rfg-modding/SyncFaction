// This is a generated file! Please edit source .ksy file and use kaitai-struct-compiler to rebuild

using System.Collections.Generic;

namespace Kaitai
{

    /// <summary>
    /// VPP_PC is a format used by Volition games
    /// </summary>
    /// <remarks>
    /// Reference: <a href="https://github.com/Moneyl/RfgToolsPlusPlus/blob/master/Documentation/Packfile.md">Source</a>
    /// </remarks>
    public partial class RfgVpp : KaitaiStruct
    {
        public static RfgVpp FromFile(string fileName)
        {
            return new RfgVpp(new KaitaiStream(fileName));
        }

        public RfgVpp(KaitaiStream p__io, KaitaiStruct p__parent = null, RfgVpp p__root = null) : base(p__io)
        {
            m_parent = p__parent;
            m_root = p__root ?? this;
            f_blockOffset = false;
            f_blockEntryData = false;
            f_blockCompactData = false;
            _read();
        }
        private void _read()
        {
            _header = new HeaderBlock(m_io, this, m_root);
            __unnamed1 = new Align(m_io, this, m_root);
            _entries = new List<Entry>();
            for (var i = 0; i < Header.NumEntries; i++)
            {
                _entries.Add(new Entry(m_io, this, m_root));
            }
            if (Header.NumEntries > 0) {
                __unnamed3 = new Align(m_io, this, m_root);
            }
            __raw_entryNames = m_io.ReadBytes(Header.LenNames);
            var io___raw_entryNames = new KaitaiStream(__raw_entryNames);
            _entryNames = new EntryNamesHolder(io___raw_entryNames, this, m_root);
            if (Header.NumEntries > 0) {
                _padBeforeData = new Align(m_io, this, m_root);
            }
            if (BlockOffset > 0) {
                __unnamed6 = m_io.ReadBytes(0);
            }
        }
        public partial class Align : KaitaiStruct
        {
            public static Align FromFile(string fileName)
            {
                return new Align(new KaitaiStream(fileName));
            }

            public Align(KaitaiStream p__io, RfgVpp p__parent = null, RfgVpp p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                f_padSize = false;
                _read();
            }
            private void _read()
            {
                __unnamed0 = m_io.ReadBytes(PadSize);
            }
            private bool f_padSize;
            private int _padSize;
            public int PadSize
            {
                get
                {
                    if (f_padSize)
                        return _padSize;
                    _padSize = (int) ((KaitaiStream.Mod(M_Io.Pos, 2048) > 0 ? (2048 - KaitaiStream.Mod(M_Io.Pos, 2048)) : 0));
                    f_padSize = true;
                    return _padSize;
                }
            }
            private byte[] __unnamed0;
            private RfgVpp m_root;
            private RfgVpp m_parent;
            public byte[] Unnamed_0 { get { return __unnamed0; } }
            public RfgVpp M_Root { get { return m_root; } }
            public RfgVpp M_Parent { get { return m_parent; } }
        }
        public partial class Entry : KaitaiStruct
        {
            public static Entry FromFile(string fileName)
            {
                return new Entry(new KaitaiStream(fileName));
            }

            public Entry(KaitaiStream p__io, RfgVpp p__parent = null, RfgVpp p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _nameOffset = m_io.ReadU4le();
                __unnamed1 = m_io.ReadBytes(4);
                _dataOffset = m_io.ReadU4le();
                _nameHash = m_io.ReadBytes(4);
                _lenData = m_io.ReadU4le();
                _lenCompressedData = m_io.ReadU4le();
                __unnamed6 = m_io.ReadBytes(4);
            }
            private uint _nameOffset;
            private byte[] __unnamed1;
            private uint _dataOffset;
            private byte[] _nameHash;
            private uint _lenData;
            private uint _lenCompressedData;
            private byte[] __unnamed6;
            private RfgVpp m_root;
            private RfgVpp m_parent;

            /// <summary>
            /// Entry name byte offset inside entry names block
            /// </summary>
            public uint NameOffset { get { return _nameOffset; } }
            public byte[] Unnamed_1 { get { return __unnamed1; } }

            /// <summary>
            /// Entry data byte offset inside entry data block
            /// </summary>
            public uint DataOffset { get { return _dataOffset; } }

            /// <summary>
            /// Entry name CRC32 hash
            /// </summary>
            public byte[] NameHash { get { return _nameHash; } }

            /// <summary>
            /// Entry data size in bytes
            /// </summary>
            public uint LenData { get { return _lenData; } }

            /// <summary>
            /// Compressed entry data size in bytes. If file is not compressed, should be 0xFFFFFFFF
            /// </summary>
            public uint LenCompressedData { get { return _lenCompressedData; } }
            public byte[] Unnamed_6 { get { return __unnamed6; } }
            public RfgVpp M_Root { get { return m_root; } }
            public RfgVpp M_Parent { get { return m_parent; } }
        }
        public partial class HeaderBlock : KaitaiStruct
        {
            public static HeaderBlock FromFile(string fileName)
            {
                return new HeaderBlock(new KaitaiStream(fileName));
            }

            public HeaderBlock(KaitaiStream p__io, RfgVpp p__parent = null, RfgVpp p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                f_isLarge = false;
                _read();
            }
            private void _read()
            {
                _magic = m_io.ReadBytes(4);
                if (!((KaitaiStream.ByteArrayCompare(Magic, new byte[] { 206, 10, 137, 81 }) == 0)))
                {
                    throw new ValidationNotEqualError(new byte[] { 206, 10, 137, 81 }, Magic, M_Io, "/types/header_block/seq/0");
                }
                _version = m_io.ReadBytes(4);
                if (!((KaitaiStream.ByteArrayCompare(Version, new byte[] { 3, 0, 0, 0 }) == 0)))
                {
                    throw new ValidationNotEqualError(new byte[] { 3, 0, 0, 0 }, Version, M_Io, "/types/header_block/seq/1");
                }
                _shortName = System.Text.Encoding.GetEncoding("ASCII").GetString(KaitaiStream.BytesTerminate(m_io.ReadBytes(65), 0, false));
                _pathName = System.Text.Encoding.GetEncoding("ASCII").GetString(KaitaiStream.BytesTerminate(m_io.ReadBytes(256), 0, false));
                __unnamed4 = m_io.ReadBytes(3);
                _flags = new HeaderFlags(m_io, this, m_root);
                __unnamed6 = m_io.ReadBytes(4);
                _numEntries = m_io.ReadU4le();
                _lenFileTotal = m_io.ReadU4le();
                _lenEntries = m_io.ReadU4le();
                _lenNames = m_io.ReadU4le();
                _lenData = m_io.ReadU4le();
                _lenCompressedData = m_io.ReadU4le();
            }
            public partial class HeaderFlags : KaitaiStruct
            {
                public static HeaderFlags FromFile(string fileName)
                {
                    return new HeaderFlags(new KaitaiStream(fileName));
                }

                public HeaderFlags(KaitaiStream p__io, RfgVpp.HeaderBlock p__parent = null, RfgVpp p__root = null) : base(p__io)
                {
                    m_parent = p__parent;
                    m_root = p__root;
                    _read();
                }
                private void _read()
                {
                    _compressed = m_io.ReadBitsIntLe(1) != 0;
                    _condensed = m_io.ReadBitsIntLe(1) != 0;
                    m_io.AlignToByte();
                    _unknownFlags = m_io.ReadBytes(3);
                    if (!((KaitaiStream.ByteArrayCompare(UnknownFlags, new byte[] { 0, 0, 0 }) == 0)))
                    {
                        throw new ValidationNotEqualError(new byte[] { 0, 0, 0 }, UnknownFlags, M_Io, "/types/header_block/types/header_flags/seq/2");
                    }
                }
                private bool _compressed;
                private bool _condensed;
                private byte[] _unknownFlags;
                private RfgVpp m_root;
                private RfgVpp.HeaderBlock m_parent;

                /// <summary>
                /// file uses ZLIB compression
                /// </summary>
                public bool Compressed { get { return _compressed; } }

                /// <summary>
                /// no padding between entries
                /// </summary>
                public bool Condensed { get { return _condensed; } }
                public byte[] UnknownFlags { get { return _unknownFlags; } }
                public RfgVpp M_Root { get { return m_root; } }
                public RfgVpp.HeaderBlock M_Parent { get { return m_parent; } }
            }
            private bool f_isLarge;
            private bool _isLarge;

            /// <summary>
            /// file length is set to 0xFFFFFF for very large archives
            /// </summary>
            public bool IsLarge
            {
                get
                {
                    if (f_isLarge)
                        return _isLarge;
                    _isLarge = (bool) (LenFileTotal == 4294967295);
                    f_isLarge = true;
                    return _isLarge;
                }
            }
            private byte[] _magic;
            private byte[] _version;
            private string _shortName;
            private string _pathName;
            private byte[] __unnamed4;
            private HeaderFlags _flags;
            private byte[] __unnamed6;
            private uint _numEntries;
            private uint _lenFileTotal;
            private uint _lenEntries;
            private uint _lenNames;
            private uint _lenData;
            private uint _lenCompressedData;
            private RfgVpp m_root;
            private RfgVpp m_parent;
            public byte[] Magic { get { return _magic; } }
            public byte[] Version { get { return _version; } }

            /// <summary>
            /// Game seems to ignore this. Always null
            /// </summary>
            public string ShortName { get { return _shortName; } }

            /// <summary>
            /// Game seems to ignore this. Always null. Might be a good spot to write packer version info for debugging
            /// </summary>
            public string PathName { get { return _pathName; } }
            public byte[] Unnamed_4 { get { return __unnamed4; } }
            public HeaderFlags Flags { get { return _flags; } }
            public byte[] Unnamed_6 { get { return __unnamed6; } }

            /// <summary>
            /// Number of files inside archive
            /// </summary>
            public uint NumEntries { get { return _numEntries; } }

            /// <summary>
            /// Total file size in bytes
            /// </summary>
            public uint LenFileTotal { get { return _lenFileTotal; } }

            /// <summary>
            /// Size of entry block in bytes. Doesn't include trailing padding bytes
            /// </summary>
            public uint LenEntries { get { return _lenEntries; } }

            /// <summary>
            /// Size of the name block in bytes. Doesn't include trailing padding bytes
            /// </summary>
            public uint LenNames { get { return _lenNames; } }

            /// <summary>
            /// Size of entry data block in bytes. Includes padding bytes between entry data
            /// </summary>
            public uint LenData { get { return _lenData; } }

            /// <summary>
            /// Size of compressed entry data in bytes. Includes padding bytes between entry data
            /// </summary>
            public uint LenCompressedData { get { return _lenCompressedData; } }
            public RfgVpp M_Root { get { return m_root; } }
            public RfgVpp M_Parent { get { return m_parent; } }
        }
        public partial class EntryNamesHolder : KaitaiStruct
        {
            public static EntryNamesHolder FromFile(string fileName)
            {
                return new EntryNamesHolder(new KaitaiStream(fileName));
            }

            public EntryNamesHolder(KaitaiStream p__io, RfgVpp p__parent = null, RfgVpp p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                f_values = false;
                _read();
            }
            private void _read()
            {
            }
            private bool f_values;
            private List<EntryName> _values;
            public List<EntryName> Values
            {
                get
                {
                    if (f_values)
                        return _values;
                    _values = new List<EntryName>();
                    for (var i = 0; i < M_Parent.Header.NumEntries; i++)
                    {
                        _values.Add(new EntryName(i, m_io, this, m_root));
                    }
                    f_values = true;
                    return _values;
                }
            }
            private RfgVpp m_root;
            private RfgVpp m_parent;
            public RfgVpp M_Root { get { return m_root; } }
            public RfgVpp M_Parent { get { return m_parent; } }
        }
        public partial class EntryName : KaitaiStruct
        {
            public EntryName(int p_i, KaitaiStream p__io, RfgVpp.EntryNamesHolder p__parent = null, RfgVpp p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _i = p_i;
                f_value = false;
                _read();
            }
            private void _read()
            {
            }
            private bool f_value;
            private string _value;
            public string Value
            {
                get
                {
                    if (f_value)
                        return _value;
                    long _pos = m_io.Pos;
                    m_io.Seek(M_Parent.M_Parent.Entries[I].NameOffset);
                    _value = System.Text.Encoding.GetEncoding("ASCII").GetString(m_io.ReadBytesTerm(0, false, true, true));
                    m_io.Seek(_pos);
                    f_value = true;
                    return _value;
                }
            }
            private int _i;
            private RfgVpp m_root;
            private RfgVpp.EntryNamesHolder m_parent;
            public int I { get { return _i; } }
            public RfgVpp M_Root { get { return m_root; } }
            public RfgVpp.EntryNamesHolder M_Parent { get { return m_parent; } }
        }
        public partial class EntryData : KaitaiStruct
        {
            public EntryData(int p_i, KaitaiStream p__io, RfgVpp.EntryDataHolder p__parent = null, RfgVpp p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _i = p_i;
                f_xDataOffset = false;
                f_xLenCompressedData = false;
                f_xNameHash = false;
                f_isLast = false;
                f_padSize = false;
                f_zlibHeader = false;
                f_xLenData = false;
                f_dataSize = false;
                f_totalSize = false;
                f_xName = false;
                _read();
            }
            private void _read()
            {
            }
            private bool f_xDataOffset;
            private uint _xDataOffset;
            public uint XDataOffset
            {
                get
                {
                    if (f_xDataOffset)
                        return _xDataOffset;
                    _xDataOffset = (uint) (M_Root.Entries[I].DataOffset);
                    f_xDataOffset = true;
                    return _xDataOffset;
                }
            }
            private bool f_xLenCompressedData;
            private uint _xLenCompressedData;
            public uint XLenCompressedData
            {
                get
                {
                    if (f_xLenCompressedData)
                        return _xLenCompressedData;
                    _xLenCompressedData = (uint) (M_Root.Entries[I].LenCompressedData);
                    f_xLenCompressedData = true;
                    return _xLenCompressedData;
                }
            }
            private bool f_xNameHash;
            private byte[] _xNameHash;
            public byte[] XNameHash
            {
                get
                {
                    if (f_xNameHash)
                        return _xNameHash;
                    _xNameHash = (byte[]) (M_Root.Entries[I].NameHash);
                    f_xNameHash = true;
                    return _xNameHash;
                }
            }
            private bool f_isLast;
            private bool _isLast;
            public bool IsLast
            {
                get
                {
                    if (f_isLast)
                        return _isLast;
                    _isLast = (bool) (I == (M_Root.Header.NumEntries - 1));
                    f_isLast = true;
                    return _isLast;
                }
            }
            private bool f_padSize;
            private int _padSize;
            public int PadSize
            {
                get
                {
                    if (f_padSize)
                        return _padSize;
                    _padSize = (int) ((M_Root.Header.Flags.Condensed ? 0 : (IsLast ? 0 : (KaitaiStream.Mod(DataSize, 2048) > 0 ? (2048 - KaitaiStream.Mod(DataSize, 2048)) : 0))));
                    f_padSize = true;
                    return _padSize;
                }
            }
            private bool f_zlibHeader;
            private Zlib _zlibHeader;
            public Zlib ZlibHeader
            {
                get
                {
                    if (f_zlibHeader)
                        return _zlibHeader;
                    if ( ((M_Root.Header.Flags.Compressed) && (M_Root.Header.Flags.Condensed == false)) ) {
                        long _pos = m_io.Pos;
                        m_io.Seek(0);
                        __raw_zlibHeader = m_io.ReadBytes(4);
                        var io___raw_zlibHeader = new KaitaiStream(__raw_zlibHeader);
                        _zlibHeader = new Zlib(io___raw_zlibHeader, this, m_root);
                        m_io.Seek(_pos);
                        f_zlibHeader = true;
                    }
                    return _zlibHeader;
                }
            }
            private bool f_xLenData;
            private uint _xLenData;
            public uint XLenData
            {
                get
                {
                    if (f_xLenData)
                        return _xLenData;
                    _xLenData = (uint) (M_Root.Entries[I].LenData);
                    f_xLenData = true;
                    return _xLenData;
                }
            }
            private bool f_dataSize;
            private uint _dataSize;
            public uint DataSize
            {
                get
                {
                    if (f_dataSize)
                        return _dataSize;
                    _dataSize = (uint) ((M_Root.Header.Flags.Compressed ? XLenCompressedData : XLenData));
                    f_dataSize = true;
                    return _dataSize;
                }
            }
            private bool f_totalSize;
            private int _totalSize;
            public int TotalSize
            {
                get
                {
                    if (f_totalSize)
                        return _totalSize;
                    _totalSize = (int) ((DataSize + PadSize));
                    f_totalSize = true;
                    return _totalSize;
                }
            }
            private bool f_xName;
            private string _xName;
            public string XName
            {
                get
                {
                    if (f_xName)
                        return _xName;
                    _xName = (string) (M_Root.EntryNames.Values[I].Value);
                    f_xName = true;
                    return _xName;
                }
            }
            private int _i;
            private RfgVpp m_root;
            private RfgVpp.EntryDataHolder m_parent;
            private byte[] __raw_zlibHeader;
            public int I { get { return _i; } }
            public RfgVpp M_Root { get { return m_root; } }
            public RfgVpp.EntryDataHolder M_Parent { get { return m_parent; } }
            public byte[] M_RawZlibHeader { get { return __raw_zlibHeader; } }
        }
        public partial class CompressedDataHolder : KaitaiStruct
        {
            public static CompressedDataHolder FromFile(string fileName)
            {
                return new CompressedDataHolder(new KaitaiStream(fileName));
            }

            public CompressedDataHolder(KaitaiStream p__io, RfgVpp p__parent = null, RfgVpp p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                f_value = false;
                f_zlibHeader = false;
                _read();
            }
            private void _read()
            {
            }
            private bool f_value;
            private byte[] _value;
            public byte[] Value
            {
                get
                {
                    if (f_value)
                        return _value;
                    _value = m_io.ReadBytesFull();
                    f_value = true;
                    return _value;
                }
            }
            private bool f_zlibHeader;
            private Zlib _zlibHeader;
            public Zlib ZlibHeader
            {
                get
                {
                    if (f_zlibHeader)
                        return _zlibHeader;
                    long _pos = m_io.Pos;
                    m_io.Seek(M_Root.BlockOffset);
                    __raw_zlibHeader = m_io.ReadBytes(4);
                    var io___raw_zlibHeader = new KaitaiStream(__raw_zlibHeader);
                    _zlibHeader = new Zlib(io___raw_zlibHeader, this, m_root);
                    m_io.Seek(_pos);
                    f_zlibHeader = true;
                    return _zlibHeader;
                }
            }
            private RfgVpp m_root;
            private RfgVpp m_parent;
            private byte[] __raw_zlibHeader;
            public RfgVpp M_Root { get { return m_root; } }
            public RfgVpp M_Parent { get { return m_parent; } }
            public byte[] M_RawZlibHeader { get { return __raw_zlibHeader; } }
        }
        public partial class Zlib : KaitaiStruct
        {
            public static Zlib FromFile(string fileName)
            {
                return new Zlib(new KaitaiStream(fileName));
            }

            public Zlib(KaitaiStream p__io, KaitaiStruct p__parent = null, RfgVpp p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                f_headerInt = false;
                f_isValid = false;
                _read();
            }
            private void _read()
            {
                _cm = m_io.ReadBitsIntLe(4);
                _cinfo = m_io.ReadBitsIntLe(4);
                _fcheck = m_io.ReadBitsIntLe(5);
                _fdict = m_io.ReadBitsIntLe(1) != 0;
                _flevel = m_io.ReadBitsIntLe(2);
            }
            private bool f_headerInt;
            private ushort _headerInt;
            public ushort HeaderInt
            {
                get
                {
                    if (f_headerInt)
                        return _headerInt;
                    long _pos = m_io.Pos;
                    m_io.Seek(0);
                    _headerInt = m_io.ReadU2be();
                    m_io.Seek(_pos);
                    f_headerInt = true;
                    return _headerInt;
                }
            }
            private bool f_isValid;
            private bool _isValid;
            public bool IsValid
            {
                get
                {
                    if (f_isValid)
                        return _isValid;
                    _isValid = (bool) ( ((Cm == 8) && (Cinfo == 7) && (KaitaiStream.Mod(HeaderInt, 31) == 0)) );
                    f_isValid = true;
                    return _isValid;
                }
            }
            private ulong _cm;
            private ulong _cinfo;
            private ulong _fcheck;
            private bool _fdict;
            private ulong _flevel;
            private RfgVpp m_root;
            private KaitaiStruct m_parent;

            /// <summary>
            /// Compression method. Should be 0x8
            /// </summary>
            public ulong Cm { get { return _cm; } }

            /// <summary>
            /// Compression info. Should be 0x7
            /// </summary>
            public ulong Cinfo { get { return _cinfo; } }

            /// <summary>
            /// Check bits for CMF and FLG. Should be a multiple of 31
            /// </summary>
            public ulong Fcheck { get { return _fcheck; } }

            /// <summary>
            /// Preset dictionary
            /// </summary>
            public bool Fdict { get { return _fdict; } }

            /// <summary>
            /// Compression level
            /// </summary>
            public ulong Flevel { get { return _flevel; } }
            public RfgVpp M_Root { get { return m_root; } }
            public KaitaiStruct M_Parent { get { return m_parent; } }
        }
        public partial class EntryDataHolder : KaitaiStruct
        {
            public static EntryDataHolder FromFile(string fileName)
            {
                return new EntryDataHolder(new KaitaiStream(fileName));
            }

            public EntryDataHolder(KaitaiStream p__io, RfgVpp p__parent = null, RfgVpp p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                f_value = false;
                _read();
            }
            private void _read()
            {
            }
            private bool f_value;
            private List<EntryData> _value;
            public List<EntryData> Value
            {
                get
                {
                    if (f_value)
                        return _value;
                    _value = new List<EntryData>();
                    for (var i = 0; i < M_Root.Header.NumEntries; i++)
                    {
                        _value.Add(new EntryData(i, m_io, this, m_root));
                    }
                    f_value = true;
                    return _value;
                }
            }
            private RfgVpp m_root;
            private RfgVpp m_parent;
            public RfgVpp M_Root { get { return m_root; } }
            public RfgVpp M_Parent { get { return m_parent; } }
        }
        private bool f_blockOffset;
        private int _blockOffset;
        public int BlockOffset
        {
            get
            {
                if (f_blockOffset)
                    return _blockOffset;
                _blockOffset = (int) (M_Io.Pos);
                f_blockOffset = true;
                return _blockOffset;
            }
        }
        private bool f_blockEntryData;
        private EntryDataHolder _blockEntryData;
        public EntryDataHolder BlockEntryData
        {
            get
            {
                if (f_blockEntryData)
                    return _blockEntryData;
                if ( ((Header.NumEntries > 0) && (!( ((M_Root.Header.Flags.Condensed == true) && (M_Root.Header.Flags.Compressed == true)) ))) ) {
                    long _pos = m_io.Pos;
                    m_io.Seek(BlockOffset);
                    _blockEntryData = new EntryDataHolder(m_io, this, m_root);
                    m_io.Seek(_pos);
                    f_blockEntryData = true;
                }
                return _blockEntryData;
            }
        }
        private bool f_blockCompactData;
        private CompressedDataHolder _blockCompactData;
        public CompressedDataHolder BlockCompactData
        {
            get
            {
                if (f_blockCompactData)
                    return _blockCompactData;
                if ( ((M_Root.Header.Flags.Condensed == true) && (M_Root.Header.Flags.Compressed == true)) ) {
                    long _pos = m_io.Pos;
                    m_io.Seek(BlockOffset);
                    _blockCompactData = new CompressedDataHolder(m_io, this, m_root);
                    m_io.Seek(_pos);
                    f_blockCompactData = true;
                }
                return _blockCompactData;
            }
        }
        private HeaderBlock _header;
        private Align __unnamed1;
        private List<Entry> _entries;
        private Align __unnamed3;
        private EntryNamesHolder _entryNames;
        private Align _padBeforeData;
        private byte[] __unnamed6;
        private RfgVpp m_root;
        private KaitaiStruct m_parent;
        private byte[] __raw_entryNames;
        public HeaderBlock Header { get { return _header; } }
        public Align Unnamed_1 { get { return __unnamed1; } }
        public List<Entry> Entries { get { return _entries; } }
        public Align Unnamed_3 { get { return __unnamed3; } }
        public EntryNamesHolder EntryNames { get { return _entryNames; } }
        public Align PadBeforeData { get { return _padBeforeData; } }

        /// <summary>
        /// hack to remember current position
        /// </summary>
        public byte[] Unnamed_6 { get { return __unnamed6; } }
        public RfgVpp M_Root { get { return m_root; } }
        public KaitaiStruct M_Parent { get { return m_parent; } }
        public byte[] M_RawEntryNames { get { return __raw_entryNames; } }
    }
}
