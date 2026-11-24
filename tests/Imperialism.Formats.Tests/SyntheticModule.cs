using System.Buffers.Binary;
using System.Text;

namespace Imperialism.Formats.Tests;

/// <summary>
/// Builds a minimal resource-only module in memory so the resource reader can be
/// tested without a copy of the original game. The layout mirrors the shipped
/// archives: one <c>RT_BITMAP</c> type directory, string-named entries, and a
/// language level below each name.
/// </summary>
internal static class SyntheticModule
{
    private const int PeHeaderOffset = 0x80;
    private const int OptionalHeaderSize = 224;
    private const int SectionHeaderOffset = PeHeaderOffset + 4 + 20 + OptionalHeaderSize;
    private const int ResourceFileOffset = 0x400;
    private const int ResourceVirtualAddress = 0x1000;

    internal sealed record Resource(int Type, string Name, int Language, byte[] Payload);

    public static byte[] Build(params Resource[] resources) => Build(resources, corruptAddress: false);

    public static byte[] BuildWithUnmappedPayload(params Resource[] resources) =>
        Build(resources, corruptAddress: true);

    private static byte[] Build(Resource[] resources, bool corruptAddress)
    {
        var tree = BuildResourceTree(resources, corruptAddress);
        var image = new byte[ResourceFileOffset + tree.Length];

        image[0] = (byte)'M';
        image[1] = (byte)'Z';
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x3C), PeHeaderOffset);

        var pe = image.AsSpan(PeHeaderOffset);
        pe[0] = (byte)'P';
        pe[1] = (byte)'E';
        var coff = pe[4..];
        BinaryPrimitives.WriteUInt16LittleEndian(coff, 0x014C);                 // i386
        BinaryPrimitives.WriteUInt16LittleEndian(coff[2..], 1);                 // one section
        BinaryPrimitives.WriteUInt16LittleEndian(coff[16..], OptionalHeaderSize);
        BinaryPrimitives.WriteUInt16LittleEndian(coff[18..], 0x2102);           // dll, 32-bit, executable

        var optional = coff[20..];
        BinaryPrimitives.WriteUInt16LittleEndian(optional, 0x010B);             // PE32
        BinaryPrimitives.WriteUInt32LittleEndian(optional[28..], 0x0040_0000);  // image base
        BinaryPrimitives.WriteUInt32LittleEndian(optional[32..], 0x1000);       // section alignment
        BinaryPrimitives.WriteUInt32LittleEndian(optional[36..], 0x200);        // file alignment
        BinaryPrimitives.WriteUInt32LittleEndian(optional[56..], (uint)(ResourceVirtualAddress + tree.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(optional[60..], ResourceFileOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(optional[68..], 2);            // subsystem: gui
        BinaryPrimitives.WriteUInt32LittleEndian(optional[92..], 16);           // data directory count
        const int ResourceDirectoryEntry = 96 + (2 * 8);
        BinaryPrimitives.WriteUInt32LittleEndian(optional[ResourceDirectoryEntry..], ResourceVirtualAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(optional[(ResourceDirectoryEntry + 4)..], (uint)tree.Length);

        var section = image.AsSpan(SectionHeaderOffset);
        Encoding.ASCII.GetBytes(".rsrc").CopyTo(section);
        BinaryPrimitives.WriteUInt32LittleEndian(section[8..], (uint)tree.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(section[12..], ResourceVirtualAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(section[16..], (uint)tree.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(section[20..], ResourceFileOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(section[36..], 0x4000_0040);   // initialized, read

        tree.CopyTo(image.AsSpan(ResourceFileOffset));
        return image;
    }

    private static byte[] BuildResourceTree(Resource[] resources, bool corruptAddress)
    {
        // One type directory, one name directory per resource, one language
        // directory below each of those, then the leaves, the name strings, and
        // the payloads.
        var typeGroups = resources.GroupBy(resource => resource.Type).OrderBy(group => group.Key).ToArray();
        using var stream = new MemoryStream();
        var layout = new Layout(typeGroups);

        WriteDirectory(stream, 0, typeGroups.Length);
        for (var typeIndex = 0; typeIndex < typeGroups.Length; typeIndex++)
        {
            WriteEntry(stream, (uint)typeGroups[typeIndex].Key, layout.NameDirectory(typeIndex), true);
        }

        for (var typeIndex = 0; typeIndex < typeGroups.Length; typeIndex++)
        {
            var group = typeGroups[typeIndex].ToArray();
            WriteDirectory(stream, group.Length, 0);
            for (var nameIndex = 0; nameIndex < group.Length; nameIndex++)
            {
                WriteEntry(
                    stream,
                    0x8000_0000u | (uint)layout.NameString(typeIndex, nameIndex),
                    layout.LanguageDirectory(typeIndex, nameIndex),
                    true);
            }
        }

        for (var typeIndex = 0; typeIndex < typeGroups.Length; typeIndex++)
        {
            var group = typeGroups[typeIndex].ToArray();
            for (var nameIndex = 0; nameIndex < group.Length; nameIndex++)
            {
                WriteDirectory(stream, 0, 1);
                WriteEntry(stream, (uint)group[nameIndex].Language, layout.Leaf(typeIndex, nameIndex), false);
            }
        }

        for (var typeIndex = 0; typeIndex < typeGroups.Length; typeIndex++)
        {
            var group = typeGroups[typeIndex].ToArray();
            for (var nameIndex = 0; nameIndex < group.Length; nameIndex++)
            {
                var address = corruptAddress
                    ? 0x7FFF_0000u
                    : (uint)(ResourceVirtualAddress + layout.Payload(typeIndex, nameIndex));
                Write(stream, address);
                Write(stream, (uint)group[nameIndex].Payload.Length);
                Write(stream, 0);
                Write(stream, 0);
            }
        }

        for (var typeIndex = 0; typeIndex < typeGroups.Length; typeIndex++)
        {
            var length = new byte[2];
            foreach (var resource in typeGroups[typeIndex])
            {
                BinaryPrimitives.WriteUInt16LittleEndian(length, (ushort)resource.Name.Length);
                stream.Write(length);
                stream.Write(Encoding.Unicode.GetBytes(resource.Name));
            }
        }

        foreach (var group in typeGroups)
        {
            foreach (var resource in group)
            {
                stream.Write(resource.Payload);
            }
        }

        return stream.ToArray();
    }

    private static void WriteDirectory(Stream stream, int namedCount, int idCount)
    {
        Span<byte> header = stackalloc byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], (ushort)namedCount);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], (ushort)idCount);
        stream.Write(header);
    }

    private static void WriteEntry(Stream stream, uint name, int offset, bool isDirectory)
    {
        Write(stream, name);
        Write(stream, isDirectory ? 0x8000_0000u | (uint)offset : (uint)offset);
    }

    private static void Write(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    /// <summary>Precomputes every offset the tree refers to, since entries point forwards.</summary>
    private sealed class Layout
    {
        private readonly IGrouping<int, Resource>[] _groups;
        private readonly int _nameDirectoriesStart;
        private readonly int _languageDirectoriesStart;
        private readonly int _leavesStart;
        private readonly int _namesStart;
        private readonly int _payloadsStart;

        public Layout(IGrouping<int, Resource>[] groups)
        {
            _groups = groups;
            var total = groups.Sum(group => group.Count());
            _nameDirectoriesStart = 16 + (groups.Length * 8);
            _languageDirectoriesStart = _nameDirectoriesStart + (groups.Length * 16) + (total * 8);
            _leavesStart = _languageDirectoriesStart + (total * (16 + 8));
            _namesStart = _leavesStart + (total * 16);
            _payloadsStart = _namesStart + groups
                .SelectMany(group => group)
                .Sum(resource => 2 + (resource.Name.Length * 2));
        }

        public int NameDirectory(int typeIndex)
        {
            var offset = _nameDirectoriesStart;
            for (var index = 0; index < typeIndex; index++)
            {
                offset += 16 + (_groups[index].Count() * 8);
            }

            return offset;
        }

        public int LanguageDirectory(int typeIndex, int nameIndex) =>
            _languageDirectoriesStart + (Flat(typeIndex, nameIndex) * (16 + 8));

        public int Leaf(int typeIndex, int nameIndex) =>
            _leavesStart + (Flat(typeIndex, nameIndex) * 16);

        public int NameString(int typeIndex, int nameIndex)
        {
            var offset = _namesStart;
            foreach (var (resource, index) in _groups.SelectMany(group => group).Select((r, i) => (r, i)))
            {
                if (index == Flat(typeIndex, nameIndex))
                {
                    return offset;
                }

                offset += 2 + (resource.Name.Length * 2);
            }

            return offset;
        }

        public int Payload(int typeIndex, int nameIndex)
        {
            var offset = _payloadsStart;
            foreach (var (resource, index) in _groups.SelectMany(group => group).Select((r, i) => (r, i)))
            {
                if (index == Flat(typeIndex, nameIndex))
                {
                    return offset;
                }

                offset += resource.Payload.Length;
            }

            return offset;
        }

        private int Flat(int typeIndex, int nameIndex)
        {
            var flat = nameIndex;
            for (var index = 0; index < typeIndex; index++)
            {
                flat += _groups[index].Count();
            }

            return flat;
        }
    }
}
