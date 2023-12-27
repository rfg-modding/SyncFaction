// This is a generated file! Please edit source .ksy file and use kaitai-struct-compiler to rebuild

using Kaitai;

namespace SyncFaction.Packer.Models.Peg
{

    /// <summary>
    /// CPEG_PC and CVBM_PC is a format used by Volition games
    /// </summary>
    /// <remarks>
    /// Reference: <a href="https://github.com/Moneyl/RfgToolsPlusPlus/blob/master/Documentation">Source</a>
    /// </remarks>
    public partial class RfgCpeg : KaitaiStruct
    {
        public static RfgCpeg FromFile(string fileName)
        {
            return new RfgCpeg(new KaitaiStream(fileName));
        }

        public RfgCpeg(KaitaiStream p__io, KaitaiStruct p__parent = null, RfgCpeg p__root = null) : base(p__io)
        {
            m_parent = p__parent;
            m_root = p__root ?? this;
            f_blockEntryData = false;
            _read();
        }
        private void _read()
        {
            _header = new HeaderBlock(m_io, this, m_root);
            _entries = new List<Entry>();
            for (var i = 0; i < Header.NumEntries; i++)
            {
                _entries.Add(new Entry(m_io, this, m_root));
            }
            _entryNames = new EntryNamesHolder(m_io, this, m_root);
        }
        public partial class Entry : KaitaiStruct
        {
            public static Entry FromFile(string fileName)
            {
                return new Entry(new KaitaiStream(fileName));
            }


            public enum BitmapFormat
            {
                None = 0,
                Bm1555 = 1,
                Bm888 = 2,
                Bm8888 = 3,
                Ps2Pal4 = 200,
                Ps2Pal8 = 201,
                Ps2Mpeg32 = 202,
                PcDxt1 = 400,
                PcDxt3 = 401,
                PcDxt5 = 402,
                Pc565 = 403,
                Pc1555 = 404,
                Pc4444 = 405,
                Pc888 = 406,
                Pc8888 = 407,
                Pc16Dudv = 408,
                Pc16Dot3Compressed = 409,
                PcA8 = 410,
                Xbox2Dxn = 600,
                Xbox2Dxt3a = 601,
                Xbox2Dxt5a = 602,
                Xbox2Ctx1 = 603,
                Ps3Dxt5n = 700,
            }
            public Entry(KaitaiStream p__io, RfgCpeg p__parent = null, RfgCpeg p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _dataOffset = m_io.ReadU4le();
                _width = m_io.ReadU2le();
                _height = m_io.ReadU2le();
                _format = ((BitmapFormat) m_io.ReadU2le());
                _sourceWidth = m_io.ReadU2le();
                _animTilesWidth = m_io.ReadU2le();
                _animTilesHeight = m_io.ReadU2le();
                _numberOfFrames = m_io.ReadBytes(2);
                if (!((KaitaiStream.ByteArrayCompare(NumberOfFrames, new byte[] { 1, 0 }) == 0)))
                {
                    throw new ValidationNotEqualError(new byte[] { 1, 0 }, NumberOfFrames, M_Io, "/types/entry/seq/7");
                }
                _flags = m_io.ReadU2le();
                _nameOffset = m_io.ReadU4le();
                _sourceHeight = m_io.ReadU2le();
                _fps = m_io.ReadBytes(1);
                if (!((KaitaiStream.ByteArrayCompare(Fps, new byte[] { 1 }) == 0)))
                {
                    throw new ValidationNotEqualError(new byte[] { 1 }, Fps, M_Io, "/types/entry/seq/11");
                }
                _mipLevels = m_io.ReadU1();
                _frameSize = m_io.ReadU4le();
                _next = m_io.ReadBytes(4);
                _previous = m_io.ReadBytes(4);
                _cache0 = m_io.ReadBytes(4);
                _cache1 = m_io.ReadBytes(4);
            }
            private uint _dataOffset;
            private ushort _width;
            private ushort _height;
            private BitmapFormat _format;
            private ushort _sourceWidth;
            private ushort _animTilesWidth;
            private ushort _animTilesHeight;
            private byte[] _numberOfFrames;
            private ushort _flags;
            private uint _nameOffset;
            private ushort _sourceHeight;
            private byte[] _fps;
            private byte _mipLevels;
            private uint _frameSize;
            private byte[] _next;
            private byte[] _previous;
            private byte[] _cache0;
            private byte[] _cache1;
            private RfgCpeg m_root;
            private RfgCpeg m_parent;

            /// <summary>
            /// Entry data byte offset inside entry data block
            /// </summary>
            public uint DataOffset { get { return _dataOffset; } }

            /// <summary>
            /// Image width
            /// </summary>
            public ushort Width { get { return _width; } }

            /// <summary>
            /// Image height
            /// </summary>
            public ushort Height { get { return _height; } }

            /// <summary>
            /// Texture format, only 3 actually used: pc_8888, pc_dxt1, pc_dxt5
            /// </summary>
            public BitmapFormat Format { get { return _format; } }

            /// <summary>
            /// TODO
            /// </summary>
            public ushort SourceWidth { get { return _sourceWidth; } }

            /// <summary>
            /// TODO
            /// </summary>
            public ushort AnimTilesWidth { get { return _animTilesWidth; } }

            /// <summary>
            /// TODO
            /// </summary>
            public ushort AnimTilesHeight { get { return _animTilesHeight; } }

            /// <summary>
            /// Always 1
            /// </summary>
            public byte[] NumberOfFrames { get { return _numberOfFrames; } }

            /// <summary>
            /// Only 3 values are actually used: no flags, srgb, has_anim_tiles. No multiple flags!
            /// </summary>
            public ushort Flags { get { return _flags; } }

            /// <summary>
            /// Entry name byte offset inside entry names block. Some files have garbage values
            /// </summary>
            public uint NameOffset { get { return _nameOffset; } }

            /// <summary>
            /// TODO
            /// </summary>
            public ushort SourceHeight { get { return _sourceHeight; } }

            /// <summary>
            /// ALways 1
            /// </summary>
            public byte[] Fps { get { return _fps; } }

            /// <summary>
            /// TODO
            /// </summary>
            public byte MipLevels { get { return _mipLevels; } }

            /// <summary>
            /// Data size without alignment
            /// </summary>
            public uint FrameSize { get { return _frameSize; } }

            /// <summary>
            /// Runtime only, can ignore
            /// </summary>
            public byte[] Next { get { return _next; } }

            /// <summary>
            /// Runtime only, can ignore
            /// </summary>
            public byte[] Previous { get { return _previous; } }

            /// <summary>
            /// Runtime only, can ignore
            /// </summary>
            public byte[] Cache0 { get { return _cache0; } }

            /// <summary>
            /// Runtime only, can ignore
            /// </summary>
            public byte[] Cache1 { get { return _cache1; } }
            public RfgCpeg M_Root { get { return m_root; } }
            public RfgCpeg M_Parent { get { return m_parent; } }
        }
        public partial class HeaderBlock : KaitaiStruct
        {
            public static HeaderBlock FromFile(string fileName)
            {
                return new HeaderBlock(new KaitaiStream(fileName));
            }

            public HeaderBlock(KaitaiStream p__io, RfgCpeg p__parent = null, RfgCpeg p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _magic = m_io.ReadBytes(4);
                if (!((KaitaiStream.ByteArrayCompare(Magic, new byte[] { 71, 69, 75, 86 }) == 0)))
                {
                    throw new ValidationNotEqualError(new byte[] { 71, 69, 75, 86 }, Magic, M_Io, "/types/header_block/seq/0");
                }
                _version = m_io.ReadBytes(2);
                if (!((KaitaiStream.ByteArrayCompare(Version, new byte[] { 10, 0 }) == 0)))
                {
                    throw new ValidationNotEqualError(new byte[] { 10, 0 }, Version, M_Io, "/types/header_block/seq/1");
                }
                _platform = m_io.ReadBytes(2);
                if (!((KaitaiStream.ByteArrayCompare(Platform, new byte[] { 0, 0 }) == 0)))
                {
                    throw new ValidationNotEqualError(new byte[] { 0, 0 }, Platform, M_Io, "/types/header_block/seq/2");
                }
                _lenFileTotal = m_io.ReadU4le();
                _lenData = m_io.ReadU4le();
                _numBitmaps = m_io.ReadU2le();
                _unknownFlags = m_io.ReadBytes(2);
                if (!((KaitaiStream.ByteArrayCompare(UnknownFlags, new byte[] { 0, 0 }) == 0)))
                {
                    throw new ValidationNotEqualError(new byte[] { 0, 0 }, UnknownFlags, M_Io, "/types/header_block/seq/6");
                }
                _numEntries = m_io.ReadU2le();
                _alignValue = m_io.ReadU2le();
            }
            private byte[] _magic;
            private byte[] _version;
            private byte[] _platform;
            private uint _lenFileTotal;
            private uint _lenData;
            private ushort _numBitmaps;
            private byte[] _unknownFlags;
            private ushort _numEntries;
            private ushort _alignValue;
            private RfgCpeg m_root;
            private RfgCpeg m_parent;
            public byte[] Magic { get { return _magic; } }
            public byte[] Version { get { return _version; } }

            /// <summary>
            /// ALways 0
            /// </summary>
            public byte[] Platform { get { return _platform; } }

            /// <summary>
            /// Total CPU file size in bytes
            /// </summary>
            public uint LenFileTotal { get { return _lenFileTotal; } }

            /// <summary>
            /// Total GPU file size in bytes
            /// </summary>
            public uint LenData { get { return _lenData; } }

            /// <summary>
            /// Always equal to entry count
            /// </summary>
            public ushort NumBitmaps { get { return _numBitmaps; } }

            /// <summary>
            /// Always 0
            /// </summary>
            public byte[] UnknownFlags { get { return _unknownFlags; } }

            /// <summary>
            /// Always equal to entry count
            /// </summary>
            public ushort NumEntries { get { return _numEntries; } }

            /// <summary>
            /// Alignment block size
            /// </summary>
            public ushort AlignValue { get { return _alignValue; } }
            public RfgCpeg M_Root { get { return m_root; } }
            public RfgCpeg M_Parent { get { return m_parent; } }
        }
        public partial class EntryNamesHolder : KaitaiStruct
        {
            public static EntryNamesHolder FromFile(string fileName)
            {
                return new EntryNamesHolder(new KaitaiStream(fileName));
            }

            public EntryNamesHolder(KaitaiStream p__io, RfgCpeg p__parent = null, RfgCpeg p__root = null) : base(p__io)
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
            private RfgCpeg m_root;
            private RfgCpeg m_parent;
            public RfgCpeg M_Root { get { return m_root; } }
            public RfgCpeg M_Parent { get { return m_parent; } }
        }
        public partial class EntryName : KaitaiStruct
        {
            public EntryName(int p_i, KaitaiStream p__io, RfgCpeg.EntryNamesHolder p__parent = null, RfgCpeg p__root = null) : base(p__io)
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

            /// <summary>
            /// File name, can be non-unique. Stored as zero-terminated string
            /// </summary>
            public string Value
            {
                get
                {
                    if (f_value)
                        return _value;
                    _value = System.Text.Encoding.GetEncoding("ASCII").GetString(m_io.ReadBytesTerm(0, false, true, true));
                    f_value = true;
                    return _value;
                }
            }
            private int _i;
            private RfgCpeg m_root;
            private RfgCpeg.EntryNamesHolder m_parent;

            /// <summary>
            /// Item order in file, starts with 0
            /// </summary>
            public int I { get { return _i; } }
            public RfgCpeg M_Root { get { return m_root; } }
            public RfgCpeg.EntryNamesHolder M_Parent { get { return m_parent; } }
        }
        public partial class EntryData : KaitaiStruct
        {
            public EntryData(int p_i, KaitaiStream p__io, RfgCpeg.EntryDataHolder p__parent = null, RfgCpeg p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _i = p_i;
                f_isLast = false;
                f_padSize = false;
                f_dataSize = false;
                f_totalSize = false;
                f_align = false;
                f_name = false;
                f_dataOffset = false;
                _read();
            }
            private void _read()
            {
            }
            private bool f_isLast;
            private bool _isLast;

            /// <summary>
            /// Is this item last in file
            /// </summary>
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

            /// <summary>
            /// Padding size to align data properly
            /// </summary>
            public int PadSize
            {
                get
                {
                    if (f_padSize)
                        return _padSize;
                    _padSize = (int) ((KaitaiStream.Mod(DataSize, Align) > 0 ? (Align - KaitaiStream.Mod(DataSize, Align)) : 0));
                    f_padSize = true;
                    return _padSize;
                }
            }
            private bool f_dataSize;
            private uint _dataSize;

            /// <summary>
            /// Data size without alignment
            /// </summary>
            public uint DataSize
            {
                get
                {
                    if (f_dataSize)
                        return _dataSize;
                    _dataSize = (uint) (M_Root.Entries[I].FrameSize);
                    f_dataSize = true;
                    return _dataSize;
                }
            }
            private bool f_totalSize;
            private int _totalSize;

            /// <summary>
            /// Data size with alignment
            /// </summary>
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
            private bool f_align;
            private ushort _align;

            /// <summary>
            /// Alignment block size
            /// </summary>
            public ushort Align
            {
                get
                {
                    if (f_align)
                        return _align;
                    _align = (ushort) (M_Root.Header.AlignValue);
                    f_align = true;
                    return _align;
                }
            }
            private bool f_name;
            private string _name;

            /// <summary>
            /// File name, can be non-unique
            /// </summary>
            public string Name
            {
                get
                {
                    if (f_name)
                        return _name;
                    _name = (string) (M_Root.EntryNames.Values[I].Value);
                    f_name = true;
                    return _name;
                }
            }
            private bool f_dataOffset;
            private uint _dataOffset;

            /// <summary>
            /// Entry data byte offset inside entry data block
            /// </summary>
            public uint DataOffset
            {
                get
                {
                    if (f_dataOffset)
                        return _dataOffset;
                    _dataOffset = (uint) (M_Root.Entries[I].DataOffset);
                    f_dataOffset = true;
                    return _dataOffset;
                }
            }
            private int _i;
            private RfgCpeg m_root;
            private RfgCpeg.EntryDataHolder m_parent;
            public int I { get { return _i; } }
            public RfgCpeg M_Root { get { return m_root; } }
            public RfgCpeg.EntryDataHolder M_Parent { get { return m_parent; } }
        }
        public partial class EntryDataHolder : KaitaiStruct
        {
            public static EntryDataHolder FromFile(string fileName)
            {
                return new EntryDataHolder(new KaitaiStream(fileName));
            }

            public EntryDataHolder(KaitaiStream p__io, RfgCpeg p__parent = null, RfgCpeg p__root = null) : base(p__io)
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
            private RfgCpeg m_root;
            private RfgCpeg m_parent;
            public RfgCpeg M_Root { get { return m_root; } }
            public RfgCpeg M_Parent { get { return m_parent; } }
        }
        private bool f_blockEntryData;
        private EntryDataHolder _blockEntryData;

        /// <summary>
        /// This data is stored in a separate .gpeg_pc file
        /// </summary>
        public EntryDataHolder BlockEntryData
        {
            get
            {
                if (f_blockEntryData)
                    return _blockEntryData;
                if (Header.NumEntries > 0) {
                    long _pos = m_io.Pos;
                    m_io.Seek(0);
                    _blockEntryData = new EntryDataHolder(m_io, this, m_root);
                    m_io.Seek(_pos);
                    f_blockEntryData = true;
                }
                return _blockEntryData;
            }
        }
        private HeaderBlock _header;
        private List<Entry> _entries;
        private EntryNamesHolder _entryNames;
        private RfgCpeg m_root;
        private KaitaiStruct m_parent;
        public HeaderBlock Header { get { return _header; } }
        public List<Entry> Entries { get { return _entries; } }
        public EntryNamesHolder EntryNames { get { return _entryNames; } }
        public RfgCpeg M_Root { get { return m_root; } }
        public KaitaiStruct M_Parent { get { return m_parent; } }
    }
}
