using Imperialism.Formats.Resources;
using Xunit;

namespace Imperialism.Formats.Tests;

public sealed class PortableExecutableResourceReaderTests
{
    [Fact]
    public void ReaderFindsStringNamedBitmapsBeneathTheirLanguage()
    {
        var image = SyntheticModule.Build(
            new SyntheticModule.Resource(2, "10000.BMP", 1033, [1, 2, 3, 4]),
            new SyntheticModule.Resource(2, "10001.BMP", 1033, [5, 6]));

        var reader = new PortableExecutableResourceReader(image);

        Assert.Equal(2, reader.Entries.Count);
        Assert.All(reader.Entries, entry => Assert.Equal(2, entry.Type.Id));
        Assert.All(reader.Entries, entry => Assert.Equal(1033, entry.Language));
        Assert.Equal(["10000.BMP", "10001.BMP"], reader.Entries.Select(entry => entry.Name.Text));
        Assert.Equal<byte[]>([1, 2, 3, 4], reader.GetData(reader.Entries[0]).ToArray());
        Assert.Equal<byte[]>([5, 6], reader.GetData(reader.Entries[1]).ToArray());
    }

    [Fact]
    public void ResourceNamesAreNotReadAsIdentifiers()
    {
        // Reading the high-bit name field as an integer is the failure that
        // silently drops every image in the shipped archives.
        var reader = new PortableExecutableResourceReader(
            SyntheticModule.Build(new SyntheticModule.Resource(2, "TITLE.BMP", 1033, [7])));

        var name = Assert.Single(reader.Entries).Name;

        Assert.Null(name.Id);
        Assert.Equal("TITLE.BMP", name.Text);
        Assert.Equal("TITLE.BMP", name.ToString());
    }

    [Fact]
    public void TypeFilterSelectsOnlyTheRequestedResourceType()
    {
        var reader = new PortableExecutableResourceReader(SyntheticModule.Build(
            new SyntheticModule.Resource(2, "IMAGE.BMP", 1033, [1]),
            new SyntheticModule.Resource(6, "TEXT", 1033, [2])));

        Assert.Single(reader.OfType(PortableExecutableResourceReader.BitmapResourceType));
        Assert.Single(reader.OfType(PortableExecutableResourceReader.StringResourceType));
    }

    [Fact]
    public void PayloadAddressesAreResolvedThroughTheSectionTable()
    {
        // The leaf's address is a relative virtual address while every other
        // offset in the tree is relative to the directory. Treating the two
        // alike yields plausible-looking bytes rather than an error, so an
        // address outside every section has to be rejected loudly.
        var image = SyntheticModule.BuildWithUnmappedPayload(
            new SyntheticModule.Resource(2, "10000.BMP", 1033, [1, 2, 3, 4]));

        var exception = Assert.Throws<InvalidDataException>(
            () => new PortableExecutableResourceReader(image));

        Assert.Contains("outside every section", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatIsNotAModuleIsRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => new PortableExecutableResourceReader([0, 1, 2, 3, 4, 5, 6, 7]));

        Assert.Contains("portable executable", exception.Message, StringComparison.Ordinal);
    }
}
