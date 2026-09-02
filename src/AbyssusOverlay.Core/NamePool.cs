using System.Text;

namespace AbyssusOverlay.Core;

public sealed class NamePool
{
    private const int BlocksOffset = 0x10;
    private const int ChunkOffsetStride = 2;
    private const int LengthShift = 6;
    private const int MaxNameLength = 1023;

    private readonly GameProcess _process;
    private readonly nint _poolAddress;

    public NamePool(GameProcess process, nint poolAddress)
    {
        _process = process;
        _poolAddress = poolAddress;
    }

    public bool TryResolve(int comparisonIndex, int number, out string name)
    {
        name = string.Empty;

        if (comparisonIndex < 0)
            return false;

        var chunkIndex = comparisonIndex >> 16;
        var chunkOffset = (comparisonIndex & 0xFFFF) * ChunkOffsetStride;

        if (!_process.TryReadPointer(_poolAddress + BlocksOffset + chunkIndex * 8, out var chunkBase) || chunkBase == 0)
            return false;

        var entryAddress = chunkBase + chunkOffset;

        var headerBuffer = new byte[2];
        if (!_process.TryReadBytes(entryAddress, headerBuffer))
            return false;

        var header = BitConverter.ToUInt16(headerBuffer, 0);
        var isWide = (header & 0x1) != 0;
        var length = header >> LengthShift;

        if (length is <= 0 or > MaxNameLength)
            return false;

        string text;
        if (isWide)
        {
            var bytes = new byte[length * 2];
            if (!_process.TryReadBytes(entryAddress + 2, bytes))
                return false;
            text = Encoding.Unicode.GetString(bytes);
        }
        else
        {
            var bytes = new byte[length];
            if (!_process.TryReadBytes(entryAddress + 2, bytes))
                return false;
            text = Encoding.ASCII.GetString(bytes);
        }

        name = number > 0 ? $"{text}_{number - 1}" : text;
        return true;
    }
}
