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
    # align is used inside too
  - id: pad_before_data
    type: align
    if: header.num_entries > 0
  - id: entry_data_block
    size: header.flags.compressed == true ? header.len_compressed_data : header.len_data
    type: entry_data_holder
    if:  _root.header.flags.condensed == false or _root.header.flags.compressed == false
  - id: compact_data
    size: header.len_compressed_data
    type: compressed_data_holder
    if: _root.header.flags.condensed == true and _root.header.flags.compressed == true

types:
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
        type: u4
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
      #adder:
      #  # https://github.com/kaitai-io/kaitai_struct/issues/291#issuecomment-997447273
      #  # this hack does not work in C# (stack overflow)
      #  type: sum_reduce( _parent.value[_index].total_size, (_index == 0) ? 0 : adder[_index-1].result )
      #  repeat: expr
      #  repeat-expr: i
      #data:
      #  pos: (i == 0 ? 0 : adder.last.result)
      #  size: data_size
      #padding:
      #  pos: (i == 0 ? 0 : adder.last.result) + data_size
      #  size: pad_size

  sum_reduce:
    params:
      - id: step_item
        type: s4
      - id: accumulator
        type: s4

    instances:
      result:
        value: step_item + accumulator
