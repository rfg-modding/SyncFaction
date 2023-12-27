meta:
  id: rfg_cpeg
  file-extension: cpeg_pc
  title: Red Faction Guerrilla CPU texture document
  endian: le
  bit-endian: le
doc: |
  CPEG_PC and CVBM_PC is a format used by Volition games
doc-ref:
  - https://github.com/Moneyl/RfgToolsPlusPlus/blob/master/Documentation
seq:
  - id: header
    type: header_block
  - id: entries
    type: entry
    repeat: expr
    repeat-expr: header.num_entries
  - id: entry_names
    type: entry_names_holder

instances:
  block_entry_data:
    pos: 0
    type: entry_data_holder
    if:  header.num_entries > 0
    doc: This data is stored in a separate .gpeg_pc file


types:
  header_block:
    seq:
    - id: magic
      contents: [0x47, 0x45, 0x4B, 0x56]
    - id: version
      contents: [0xA, 0]
    - id: platform
      contents: [0, 0]
      doc: ALways 0
    - id: len_file_total
      type: u4
      doc: Total CPU file size in bytes
    - id: len_data
      type: u4
      doc: Total GPU file size in bytes
      doc: Size of data block in bytes
    - id: num_bitmaps
      type: u2
      doc: Always equal to entry count
      doc: Number of bitmaps inside document
    - id: unknown_flags
      contents: [0, 0]
      doc: Always 0
    - id: num_entries
      type: u2
      doc: Always equal to entry count
      doc: Number of entries inside document
    - id: align_value
      type: u2
      doc: Alignment block size
  entry:
    seq:
      - id: data_offset
        type: u4
        doc: Entry data byte offset inside entry data block
      - id: width
        type: u2
        doc: Image width
      - id: height
        type: u2
        doc: Image height
      - id: format
        type: u2
        enum: bitmap_format
        doc: Texture format, only 3 actually used: pc_8888, pc_dxt1, pc_dxt5
      - id: source_width
        type: u2
        doc: TODO
      - id: anim_tiles_width
        type: u2
        doc: TODO
      - id: anim_tiles_height
        type: u2
        doc: TODO
      - id: number_of_frames
        contents: [1, 0]
        doc: Always 1
      - id: flags
        type: u2
        doc: Only 3 values are actually used: no flags, srgb, has_anim_tiles. No multiple flags!
      - id: name_offset
        type: u4
        doc: Entry name byte offset inside entry names block. Some files have garbage values
      - id: source_height
        type: u2
        doc: TODO
      - id: fps
        contents: [1]
        doc: ALways 1
      - id: mip_levels
        type: u1
        doc: TODO
      - id: frame_size
        type: u4
        doc: Data size without alignment
      - id: next
        size: 4
        doc: Runtime only, can ignore
      - id: previous
        size: 4
        doc: Runtime only, can ignore
      - id: cache0
        size: 4
        doc: Runtime only, can ignore
      - id: cache1
        size: 4
        doc: Runtime only, can ignore
    enums:
      bitmap_format:
        0: none
        1: bm_1555
        2: bm_888
        3: bm_8888
        200: ps2_pal4
        201: ps2_pal8
        202: ps2_mpeg32
        400: pc_dxt1
        401: pc_dxt3
        402: pc_dxt5
        403: pc_565
        404: pc_1555
        405: pc_4444
        406: pc_888
        407: pc_8888
        408: pc_16_dudv
        409: pc_16_dot3_compressed
        410: pc_a8
        600: xbox2_dxn
        601: xbox2_dxt3a
        602: xbox2_dxt5a
        603: xbox2_ctx1
        700: ps3_dxt5n
  entry_names_holder:
    # this type is required to create a substream
    # relies on reading by zero terminators only
    # as there are no cross-references to this data
    instances:
      values:
        type: entry_name(_index)
        repeat: expr
        repeat-expr: _parent.header.num_entries
  entry_name:
    params:
      - id: i
        type: s4
        doc: Item order in file, starts with 0
    instances:
      value:
        type: strz
        encoding: ASCII
        doc: File name, can be non-unique. Stored as zero-terminated string
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
      name:
        value: _root.entry_names.values[i].value
        doc: File name, can be non-unique
      data_offset:
        value: _root.entries[i].data_offset
        doc: Entry data byte offset inside entry data block
      data_size:
        value: _root.entries[i].frame_size
        doc: Data size without alignment
      # calculate useful values
      is_last:
        value: i == _root.header.num_entries-1
        doc: Is this item last in file
      align:
        value: _root.header.align_value
        doc: Alignment block size
      pad_size:
        # NOTE: looks like last entry should be padded too
        #value: is_last ? 0 : (data_size % align) > 0 ? align - (data_size % align) : 0
        value: (data_size % align) > 0 ? align - (data_size % align) : 0
        doc: Padding size to align data properly
      total_size:
        value: data_size + pad_size
        doc: Data size with alignment
