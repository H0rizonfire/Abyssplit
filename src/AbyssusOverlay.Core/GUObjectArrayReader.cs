using System.Collections.Generic;

namespace AbyssusOverlay.Core;

public sealed class GUObjectArrayReader
{
    private const int ObjObjectsOffset = 0x10;
    private const int ChunksPtrOffset = 0x00;
    private const int NumElementsOffset = 0x14;
    private const int ElementsPerChunk = 64 * 1024;
    private const int ObjectItemSize = 0x18;
    private const int ObjectItemObjectOffset = 0x00;

    private const int ObjectFlagsOffset = 0x08;
    private const int ClassPrivateOffset = 0x10;
    private const int NamePrivateOffset = 0x18;

    private const int RF_ClassDefaultObject = 0x10;

    private readonly GameProcess _process;
    private readonly nint _objObjectsAddress;

    public GUObjectArrayReader(GameProcess process, nint gUObjectArrayAddress)
    {
        _process = process;
        _objObjectsAddress = gUObjectArrayAddress + ObjObjectsOffset;
    }

    public bool TryGetObjectCount(out int numElements)
    {
        return _process.TryReadInt32(_objObjectsAddress + NumElementsOffset, out numElements);
    }

    public IEnumerable<nint> EnumerateObjects() => EnumerateObjects(0);

    public IEnumerable<nint> EnumerateObjects(int startIndex)
    {
        if (!_process.TryReadPointer(_objObjectsAddress + ChunksPtrOffset, out var chunksArray) || chunksArray == 0)
            yield break;

        if (!_process.TryReadInt32(_objObjectsAddress + NumElementsOffset, out var numElements) || numElements <= startIndex)
            yield break;

        var numChunks = (numElements + ElementsPerChunk - 1) / ElementsPerChunk;
        var startChunk = Math.Max(0, startIndex) / ElementsPerChunk;

        for (var chunkIndex = startChunk; chunkIndex < numChunks; chunkIndex++)
        {
            if (!_process.TryReadPointer(chunksArray + chunkIndex * 8, out var chunk) || chunk == 0)
                continue;

            var chunkStartIndex = chunkIndex * ElementsPerChunk;
            var elementsInThisChunk = Math.Min(ElementsPerChunk, numElements - chunkStartIndex);
            var startWithinChunk = chunkIndex == startChunk ? Math.Max(0, startIndex - chunkStartIndex) : 0;

            for (var i = startWithinChunk; i < elementsInThisChunk; i++)
            {
                if (_process.TryReadPointer(chunk + i * ObjectItemSize + ObjectItemObjectOffset, out var obj) && obj != 0)
                    yield return obj;
            }
        }
    }

    public bool TryGetClassName(nint objectAddress, NamePool namePool, out string className)
    {
        className = string.Empty;

        if (!_process.TryReadPointer(objectAddress + ClassPrivateOffset, out var classPtr) || classPtr == 0)
            return false;

        if (!_process.TryReadFName(classPtr + NamePrivateOffset, out var comparisonIndex, out var number))
            return false;

        return namePool.TryResolve(comparisonIndex, number, out className);
    }

    public bool TryGetOwnName(nint objectAddress, NamePool namePool, out string name)
    {
        name = string.Empty;

        if (!_process.TryReadFName(objectAddress + NamePrivateOffset, out var comparisonIndex, out var number))
            return false;

        return namePool.TryResolve(comparisonIndex, number, out name);
    }

    public bool TryGetClassPointer(nint objectAddress, out nint classPointer)
    {
        return _process.TryReadPointer(objectAddress + ClassPrivateOffset, out classPointer) && classPointer != 0;
    }

    public nint? FindClassPointerByName(string className, NamePool namePool)
    {
        foreach (var obj in EnumerateObjects())
        {
            if (TryGetOwnName(obj, namePool, out var name) && name == className)
                return obj;
        }

        return null;
    }

    public bool IsClassDefaultObject(nint objectAddress)
    {
        return _process.TryReadInt32(objectAddress + ObjectFlagsOffset, out var flags)
            && (flags & RF_ClassDefaultObject) != 0;
    }

    public nint? FindFirstByClassName(string className, NamePool namePool)
    {
        foreach (var obj in EnumerateObjects())
        {
            if (IsClassDefaultObject(obj))
                continue;

            if (TryGetClassName(obj, namePool, out var name) && name == className)
                return obj;
        }

        return null;
    }

    public nint? FindFirstByClassNameSuffix(string suffix, NamePool namePool)
    {
        foreach (var obj in EnumerateObjects())
        {
            if (IsClassDefaultObject(obj))
                continue;

            if (TryGetClassName(obj, namePool, out var name) && name.EndsWith(suffix, StringComparison.Ordinal))
                return obj;
        }

        return null;
    }

    public IEnumerable<nint> EnumerateObjectsWithClassIn(IReadOnlySet<nint> classPointers, int startIndex = 0)
    {
        foreach (var obj in EnumerateObjects(startIndex))
        {
            if (TryGetClassPointer(obj, out var classPointer) && classPointers.Contains(classPointer))
                yield return obj;
        }
    }

    public IEnumerable<(nint Address, string ClassName)> FindAllByClassNameContains(
        string substring, NamePool namePool, bool caseSensitive = false)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        foreach (var obj in EnumerateObjects())
        {
            if (IsClassDefaultObject(obj))
                continue;

            if (TryGetClassName(obj, namePool, out var name) && name.Contains(substring, comparison))
                yield return (obj, name);
        }
    }
}
