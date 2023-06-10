meta:
  id: rfg_vpp
  file-extension: vpp_pc
  title: Red Faction Guerrilla archive
  endian: le
  bit-endian: le
doc: |
  VPP_PC is a format used by Volition games
doc-ref:
  - https://github.com/Moneyl/RfgToolsPlusPlus/blob/master/Documentation/Packfile.md

seq:
  - id: header
    type: header_block
  - type: align
  - id: entries
    type: entry
    repeat: expr
    repeat-expr: header.num_entries
  - type: align
    if: header.num_entries > 0
  - id: entry_names
    size: header.len_names
    type: entry_names_holder
  - id: pad_before_data
    type: align
    if: header.num_entries > 0
  - doc: hack to remember current position
    size: 0
    if: block_offset > 0

instances:
  block_offset:
    value: _io.pos

  block_entry_data:
    #size: header.flags.compressed == true ? header.len_compressed_data : header.len_data
    pos: block_offset
    type: entry_data_holder
    if:  header.num_entries > 0 and not (_root.header.flags.condensed == true and _root.header.flags.compressed == true)

  block_compact_data:
    #size: header.len_compressed_data
    pos: block_offset
    type: compressed_data_holder
    if: _root.header.flags.condensed == true and _root.header.flags.compressed == true

types:
  zlib:
    seq:
      - id: cm
        type: b4
        doc: Compression method. Should be 0x8
      - id: cinfo
        type: b4
        doc: Compression info. Should be 0x7
      - id: fcheck
        type: b5
        doc: Check bits for CMF and FLG. Should be a multiple of 31
      - id: fdict
        type: b1
        doc: Preset dictionary
      - id: flevel
        type: b2
        doc: Compression level
    instances:
      header_int:
        pos: 0
        type: u2be
      is_valid:
        value: cm == 0x8 and cinfo == 0x7 and header_int % 31 == 0
  align:
    seq:
      - size: pad_size
    instances:
      pad_size:
        value: (_io.pos % 2048) > 0 ? 2048 - (_io.pos % 2048) : 0

  compressed_data_holder:
    instances:
      value:
        size-eos: true
      zlib_header:
        type: zlib
        pos: _root.block_offset
        size: 4

  header_block:
    seq:
    - id: magic
      contents: [0xCE, 0x0A, 0x89, 0x51]
    - id: version
      contents: [0x3, 0, 0, 0]
    - id: short_name
      type: strz
      size: 65
      doc: Game seems to ignore this. Always null
      encoding: ASCII
    - id: path_name
      type: strz
      size: 256
      doc: Game seems to ignore this. Always null. Might be a good spot to write packer version info for debugging
      encoding: ASCII
    - size: 3
    - id: flags
      type: header_flags
    - size: 4
    - id: num_entries
      type: u4
      doc: Number of files inside archive
    - id: len_file_total
      type: u4
      doc: Total file size in bytes
    - id: len_entries
      type: u4
      doc: Size of entry block in bytes. Doesn't include trailing padding bytes
    - id: len_names
      type: u4
      doc: Size of the name block in bytes. Doesn't include trailing padding bytes
    - id: len_data
      type: u4
      doc: Size of entry data block in bytes. Includes padding bytes between entry data
    - id: len_compressed_data
      type: u4
      doc: Size of compressed entry data in bytes. Includes padding bytes between entry data
    instances:
      is_large:
        value: len_file_total == 4294967295
        doc: file length is set to 0xFFFFFF for very large archives

    types:
      header_flags:
        seq:
          - id: compressed
            type: b1
            doc: file uses ZLIB compression
          - id: condensed
            type: b1
            doc: no padding between entries
          - id: unknown_flags
            contents: [0, 0, 0]

  entry:
    seq:
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

  entry_names_holder:
    # this type is required to create a substream
    # we also dont want to rely on reading by zero terminators only
    # so here is a solution using explicit offsets
    instances:
      values:
        type: entry_name(_index)
        repeat: expr
        repeat-expr: _parent.header.num_entries

  entry_name:
    params:
      - id: i
        type: s4
    instances:
      value:
        type: strz
        encoding: ASCII
        pos: _parent._parent.entries[i].name_offset

  entry_data_holder:
    instances:
      value:
        repeat: expr
        repeat-expr: _root.header.num_entries
        type: entry_data(_index)

  entry_data:
    params:
      - id: i
        type: s4
    #seq:
    #  - id: wtf
    #    doc: hack to remember current data position
    #    size: 0
    #    if: wtf_offset > 0
    instances:
      # copy stuff from other places for convenience
      x_name:
        value: _root.entry_names.values[i].value
      x_data_offset:
        value: _root.entries[i].data_offset
      x_name_hash:
        value: _root.entries[i].name_hash
      x_len_data:
        value: _root.entries[i].len_data
      x_len_compressed_data:
        value: _root.entries[i].len_compressed_data
      # calculate useful values
      is_last:
        value: i == _root.header.num_entries-1
      data_size:
        value: _root.header.flags.compressed ? x_len_compressed_data : x_len_data
      pad_size:
        value: _root.header.flags.condensed ? 0 : is_last ? 0 : (data_size % 2048) > 0 ? 2048 - (data_size % 2048) : 0
      total_size:
        value: data_size + pad_size
      zlib_header:
        if: _root.header.flags.compressed and _root.header.flags.condensed == false
        type: zlib
        pos: 0
        size: 4
