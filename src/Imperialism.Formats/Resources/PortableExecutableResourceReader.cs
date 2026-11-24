using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;

namespace Imperialism.Formats.Resources;

/// <summary>One leaf of a portable executable's resource tree.</summary>
public sealed record ResourceEntry(
    ResourceName Type,
    ResourceName Name,
    int Language,
    int DataOffset,
    int DataSize);

/// <summary>
/// Reads the resource tree out of a resource-only Win32 module. The original
/// game's <c>.gob</c> archives are exactly that: every one begins <c>MZ</c>, and
/// the resource data directory covers essentially the whole file.
/// </summary>
/// <remarks>
/// The platform's own <c>LoadLibraryEx</c> plus <c>EnumResourceNames</c> would be
/// shorter and is not used, because it is Windows-only and CI builds this
/// solution on Linux. <c>System.Drawing.Common</c> is unavailable for the same
/// reason. The walk below is the whole cost of staying portable.
/// </remarks>
public sealed class PortableExecutableResourceReader
{
    /// <summary>The <c>RT_BITMAP</c> resource type, which is what the art archives hold.</summary>
    public const int BitmapResourceType = 2;

    /// <summary>The <c>RT_STRING</c> resource type, which is what <c>STR#ENU.GOB</c> holds.</summary>
    public const int StringResourceType = 6;

    private const int DirectoryHeaderSize = 16;
    private const int DirectoryEntrySize = 8;
    private const int DataEntrySize = 16;
    private const uint HighBit = 0x8000_0000u;

    private readonly ReadOnlyMemory<byte> _image;

    public PortableExecutableResourceReader(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        _image = image;

        int resourceDirectoryOffset;
        ImmutableArray<SectionHeader> sections;
        try
        {
            using var reader = new PEReader(ImmutableCollectionsMarshal.AsImmutableArray(image));
            var headers = reader.PEHeaders;
            if (headers.PEHeader is null)
            {
                throw new InvalidDataException("The module has no optional header.");
            }

            if (!headers.TryGetDirectoryOffset(
                    headers.PEHeader.ResourceTableDirectory,
                    out resourceDirectoryOffset))
            {
                throw new InvalidDataException("The module has no resource directory.");
            }

            sections = headers.SectionHeaders;
        }
        catch (BadImageFormatException exception)
        {
            throw new InvalidDataException("The file is not a portable executable.", exception);
        }

        _sections = sections;
        var entries = new List<ResourceEntry>();
        WalkTypeDirectory(resourceDirectoryOffset, entries);
        Entries = Array.AsReadOnly(entries.ToArray());
    }

    private readonly ImmutableArray<SectionHeader> _sections;

    /// <summary>Every leaf in the tree, in the order the module stores them.</summary>
    public IReadOnlyList<ResourceEntry> Entries { get; }

    public ReadOnlyMemory<byte> GetData(ResourceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return _image.Slice(entry.DataOffset, entry.DataSize);
    }

    public IEnumerable<ResourceEntry> OfType(int resourceType) =>
        Entries.Where(entry => entry.Type.Matches(resourceType));

    private void WalkTypeDirectory(int resourceRoot, List<ResourceEntry> entries)
    {
        foreach (var typeEntry in ReadDirectory(resourceRoot, resourceRoot))
        {
            if (!typeEntry.IsDirectory)
            {
                // A leaf directly under the root has no name and no language. The
                // shipped archives never do this; skipping is kinder than throwing
                // on a file we only ever read.
                continue;
            }

            foreach (var nameEntry in ReadDirectory(resourceRoot, resourceRoot + typeEntry.Offset))
            {
                if (!nameEntry.IsDirectory)
                {
                    entries.Add(ReadLeaf(typeEntry.Name, nameEntry.Name, 0, resourceRoot + nameEntry.Offset));
                    continue;
                }

                foreach (var languageEntry in ReadDirectory(
                             resourceRoot,
                             resourceRoot + nameEntry.Offset))
                {
                    if (languageEntry.IsDirectory)
                    {
                        throw new InvalidDataException(
                            "The resource tree is deeper than type, name, and language.");
                    }

                    entries.Add(ReadLeaf(
                        typeEntry.Name,
                        nameEntry.Name,
                        languageEntry.Name.Id ?? 0,
                        resourceRoot + languageEntry.Offset));
                }
            }
        }
    }

    private ResourceEntry ReadLeaf(ResourceName type, ResourceName name, int language, int offset)
    {
        var leaf = Read(offset, DataEntrySize);

        // This field is a relative virtual address, unlike every other offset in
        // the tree, which is relative to the start of the resource directory.
        // Mixing the two yields plausible-looking garbage rather than an error.
        var dataAddress = BinaryPrimitives.ReadUInt32LittleEndian(leaf);
        var size = BinaryPrimitives.ReadUInt32LittleEndian(leaf[4..]);
        var dataOffset = ResolveAddress(dataAddress);
        if (size > int.MaxValue || dataOffset + (long)size > _image.Length)
        {
            throw new InvalidDataException($"Resource '{name}' runs past the end of the module.");
        }

        return new ResourceEntry(type, name, language, dataOffset, (int)size);
    }

    private int ResolveAddress(uint address)
    {
        foreach (var section in _sections)
        {
            var start = (uint)section.VirtualAddress;
            var length = (uint)Math.Max(section.VirtualSize, section.SizeOfRawData);
            if (address >= start && address < start + length)
            {
                return (int)(address - start) + section.PointerToRawData;
            }
        }

        throw new InvalidDataException(
            $"Relative virtual address 0x{address:X8} falls outside every section.");
    }

    private List<DirectoryEntry> ReadDirectory(int resourceRoot, int offset)
    {
        var header = Read(offset, DirectoryHeaderSize);
        int namedCount = BinaryPrimitives.ReadUInt16LittleEndian(header[12..]);
        int idCount = BinaryPrimitives.ReadUInt16LittleEndian(header[14..]);
        var total = namedCount + idCount;
        var entries = new List<DirectoryEntry>(total);
        for (var index = 0; index < total; index++)
        {
            var raw = Read(offset + DirectoryHeaderSize + (index * DirectoryEntrySize), DirectoryEntrySize);
            var nameField = BinaryPrimitives.ReadUInt32LittleEndian(raw);
            var offsetField = BinaryPrimitives.ReadUInt32LittleEndian(raw[4..]);
            var name = (nameField & HighBit) != 0
                ? ResourceName.FromText(ReadName(resourceRoot + (int)(nameField & ~HighBit)))
                : ResourceName.FromId((int)nameField);
            entries.Add(new DirectoryEntry(
                name,
                (int)(offsetField & ~HighBit),
                (offsetField & HighBit) != 0));
        }

        return entries;
    }

    private string ReadName(int offset)
    {
        var lengthField = Read(offset, 2);
        int characterCount = BinaryPrimitives.ReadUInt16LittleEndian(lengthField);
        var text = Read(offset + 2, characterCount * 2);
        return Encoding.Unicode.GetString(text);
    }

    private ReadOnlySpan<byte> Read(int offset, int length)
    {
        if (offset < 0 || length < 0 || (long)offset + length > _image.Length)
        {
            throw new InvalidDataException(
                $"The resource tree reads {length} bytes at {offset}, past the end of the module.");
        }

        return _image.Span.Slice(offset, length);
    }

    private readonly record struct DirectoryEntry(ResourceName Name, int Offset, bool IsDirectory);
}
