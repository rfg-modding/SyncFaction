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
            if ( ((M_Root.Header.Flags.Condensed == false) || (M_Root.Header.Flags.Compressed == false)) ) {
                __raw_entryDataBlock = m_io.ReadBytes((Header.Flags.Compressed == true ? Header.LenCompressedData : Header.LenData));
                var io___raw_entryDataBlock = new KaitaiStream(__raw_entryDataBlock);
                _entryDataBlock = new EntryDataHolder(io___raw_entryDataBlock, this, m_root);
            }
            if ( ((M_Root.Header.Flags.Condensed == true) && (M_Root.Header.Flags.Compressed == true)) ) {
                __raw_compactData = m_io.ReadBytes(Header.LenCompressedData);
                var io___raw_compactData = new KaitaiStream(__raw_compactData);
                _compactData = new CompressedDataHolder(io___raw_compactData, this, m_root);
            }
        }
        public partial class SumReduce : KaitaiStruct
        {
            public SumReduce(int p_stepItem, int p_accumulator, KaitaiStream p__io, KaitaiStruct p__parent = null, RfgVpp p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _stepItem = p_stepItem;
                _accumulator = p_accumulator;
                f_result = false;
                _read();
            }
            private void _read()
            {
            }
            private bool f_result;
            private int _result;
            public int Result
            {
                get
                {
                    if (f_result)
                        return _result;
                    _result = (int) ((StepItem + Accumulator));
                    f_result = true;
                    return _result;
                }
            }
            private int _stepItem;
            private int _accumulator;
            private RfgVpp m_root;
            private KaitaiStruct m_parent;
            public int StepItem { get { return _stepItem; } }
            public int Accumulator { get { return _accumulator; } }
            public RfgVpp M_Root { get { return m_root; } }
            public KaitaiStruct M_Parent { get { return m_parent; } }
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
                _nameHash = m_io.ReadU4le();
                _lenData = m_io.ReadU4le();
                _lenCompressedData = m_io.ReadU4le();
                __unnamed6 = m_io.ReadBytes(4);
            }
            private uint _nameOffset;
            private byte[] __unnamed1;
            private uint _dataOffset;
            private uint _nameHash;
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
            public uint NameHash { get { return _nameHash; } }

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
            private uint _xNameHash;
            public uint XNameHash
            {
                get
                {
                    if (f_xNameHash)
                        return _xNameHash;
                    _xNameHash = (uint) (M_Root.Entries[I].NameHash);
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
            public int I { get { return _i; } }
            public RfgVpp M_Root { get { return m_root; } }
            public RfgVpp.EntryDataHolder M_Parent { get { return m_parent; } }
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
            private RfgVpp m_root;
            private RfgVpp m_parent;
            public RfgVpp M_Root { get { return m_root; } }
            public RfgVpp M_Parent { get { return m_parent; } }
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
        private HeaderBlock _header;
        private Align __unnamed1;
        private List<Entry> _entries;
        private Align __unnamed3;
        private EntryNamesHolder _entryNames;
        private Align _padBeforeData;
        private EntryDataHolder _entryDataBlock;
        private CompressedDataHolder _compactData;
        private RfgVpp m_root;
        private KaitaiStruct m_parent;
        private byte[] __raw_entryNames;
        private byte[] __raw_entryDataBlock;
        private byte[] __raw_compactData;
        public HeaderBlock Header { get { return _header; } }
        public Align Unnamed_1 { get { return __unnamed1; } }
        public List<Entry> Entries { get { return _entries; } }
        public Align Unnamed_3 { get { return __unnamed3; } }
        public EntryNamesHolder EntryNames { get { return _entryNames; } }
        public Align PadBeforeData { get { return _padBeforeData; } }
        public EntryDataHolder EntryDataBlock { get { return _entryDataBlock; } }
        public CompressedDataHolder CompactData { get { return _compactData; } }
        public RfgVpp M_Root { get { return m_root; } }
        public KaitaiStruct M_Parent { get { return m_parent; } }
        public byte[] M_RawEntryNames { get { return __raw_entryNames; } }
        public byte[] M_RawEntryDataBlock { get { return __raw_entryDataBlock; } }
        public byte[] M_RawCompactData { get { return __raw_compactData; } }
    }
}
