using DirectXTexNet;

namespace SyncFaction.Packer.Services.Peg;

record DxFormatInfo(DXGI_FORMAT DxFormat, bool Compressed, bool Extended, int CompressionRatio);