using Imperialism.Formats;
using Xunit;

namespace Imperialism.Formats.Tests;

public sealed class LegacyMapCodecTests
{
    [Fact]
    public void ImperialismProfileIsTheOnlyLegacyDimensionDefault()
    {
        var profile = MapFormatProfile.Imperialism1;

        Assert.Equal(108, profile.Width);
        Assert.Equal(60, profile.Height);
        Assert.Equal(6_480, profile.CellCount);
        Assert.Equal(309_312, profile.FileSize);
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(1, 0, 0, 0)]
    [InlineData(1, 1, -1, 1)]
    [InlineData(1, 1, 1, -1)]
    [InlineData(1, 1, 1, 0)]
    public void ProfileRejectsInvalidDimensions(
        int width,
        int height,
        int trailerCount,
        int trailerSize)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new MapFormatProfile(width, height, trailerCount, trailerSize));
    }

    [Fact]
    public void ProfileUsesCheckedSizeArithmetic()
    {
        Assert.Throws<OverflowException>(
            () => new MapFormatProfile(int.MaxValue, 2, 0, 0));
    }

    [Fact]
    public void EveryCellByteHasAnExplicitRoundTripField()
    {
        var raw = Enumerable.Range(0, HexCell.Size).Select(static value => (byte)value).ToArray();

        var cell = HexCell.Decode(raw);

        Assert.Equal(0, cell.TerrainUnderlay);
        Assert.Equal(1, cell.OceanCoastline);
        Assert.Equal(2, cell.River);
        Assert.Equal(3, cell.NationZoneA);
        Assert.Equal(4, cell.NationZoneB);
        Assert.Equal(5, cell.Unused05);
        Assert.Equal(6, cell.Rail);
        Assert.Equal(7, cell.NationalBorder);
        Assert.Equal(8, cell.ProvinceBorder);
        Assert.Equal(9, cell.LandCoastline);
        Assert.Equal(10, cell.LikeCellAdjacency);
        Assert.Equal(11, cell.HillMountainOverlay);
        Assert.Equal(12, cell.Unused12);
        Assert.Equal(13, cell.Unused13);
        Assert.Equal(14, cell.Unused14);
        Assert.Equal(15, cell.Unused15);
        Assert.Equal(16, cell.Unknown16);
        Assert.Equal(17, cell.ResourceA);
        Assert.Equal(18, cell.ResourceB);
        Assert.Equal(19, cell.Terrain);
        Assert.Equal(0x1415, cell.Province);
        Assert.Equal(22, cell.Unused22);
        Assert.Equal(23, cell.Unused23);
        Assert.Equal(24, cell.Unused24);
        Assert.Equal(25, cell.Unused25);
        Assert.Equal(26, cell.Unused26);
        Assert.Equal(27, cell.Unused27);
        Assert.Equal(28, cell.Unused28);
        Assert.Equal(29, cell.TownType);
        Assert.Equal(30, cell.Unused30);
        Assert.Equal(31, cell.Unused31);
        Assert.Equal(32, cell.Unused32);
        Assert.Equal(33, cell.Unused33);
        Assert.Equal(34, cell.Unused34);
        Assert.Equal(35, cell.Unused35);
        Assert.Equal(raw, cell.Encode());
    }

    [Fact]
    public void ArbitraryDimensionsRoundTripWithoutALegacyTrailer()
    {
        var profile = new MapFormatProfile(7, 5, 0, 0);
        var map = MapDocument.CreateBlank(profile);
        map[6, 4] = new HexCell { Terrain = 8, Province = 9 };

        var decoded = LegacyMapCodec.Decode(LegacyMapCodec.Encode(map), profile);

        Assert.Equal(35, decoded.Cells.Count);
        Assert.Equal(9, decoded[6, 4].Province);
        Assert.Equal(8, decoded[6, 4].Terrain);
    }

    [Fact]
    public void LargerNonLegacyMapHasNoFixedCellCountAssumption()
    {
        var profile = new MapFormatProfile(257, 129, 0, 0);
        var map = MapDocument.CreateBlank(profile);

        var decoded = LegacyMapCodec.Decode(LegacyMapCodec.Encode(map), profile);

        Assert.Equal(33_153, decoded.Cells.Count);
        Assert.Equal(ushort.MaxValue, decoded[256, 128].Province);
    }

    [Fact]
    public void TrailerBytesArePreservedExactly()
    {
        var profile = new MapFormatProfile(1, 1, 2, 3);
        var trailer = new byte[] { 9, 8, 7, 6, 5, 4 };
        var map = new MapDocument(profile, new[] { new HexCell() }, trailer);

        var encoded = LegacyMapCodec.Encode(map);
        var decoded = LegacyMapCodec.Decode(encoded, profile);

        Assert.Equal(trailer, decoded.TrailerBytes.ToArray());
        Assert.Equal(encoded, LegacyMapCodec.Encode(decoded));
    }

    [Fact]
    public void WrongFileLengthIsRejected()
    {
        var profile = new MapFormatProfile(2, 2, 0, 0);

        var exception = Assert.Throws<InvalidDataException>(
            () => LegacyMapCodec.Decode(new byte[3 * HexCell.Size], profile));

        Assert.Contains("Expected 144 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CoordinatesAreBoundsChecked()
    {
        var map = MapDocument.CreateBlank(new MapFormatProfile(2, 2, 0, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = map[-1, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = map[0, 2]);
    }

    [Fact]
    public void FileHelpersRoundTripGeneratedMap()
    {
        var profile = new MapFormatProfile(3, 2, 0, 0);
        var map = MapDocument.CreateBlank(profile);
        var path = Path.Combine(Path.GetTempPath(), $"imperialism-{Guid.NewGuid():N}.bin");
        try
        {
            LegacyMapCodec.Save(path, map);
            var loaded = LegacyMapCodec.Load(path, profile);
            Assert.Equal(LegacyMapCodec.Encode(map), LegacyMapCodec.Encode(loaded));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void OriginalMapsRoundTripWhenLocalCorpusIsConfigured()
    {
        var directory = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var paths = Directory.GetFiles(directory, "*.map")
            .Where(IsNumberedScenarioFile)
            .Order(StringComparer.Ordinal)
            .ToArray();
        // At least the ten originals. A live Scenario folder also holds worlds
        // this project generated into it, which must round-trip too; demanding
        // exactly ten fails on any install that has been used.
        Assert.True(paths.Length >= 10, $"Expected the corpus, found {paths.Length} maps.");
        foreach (var path in paths)
        {
            var original = File.ReadAllBytes(path);
            var decoded = LegacyMapCodec.Decode(original);
            Assert.Equal(original, LegacyMapCodec.Encode(decoded));
        }
    }

    private static bool IsNumberedScenarioFile(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        return stem.Length > 1 && stem[0] == 's' && int.TryParse(stem.AsSpan(1), out _);
    }
}
