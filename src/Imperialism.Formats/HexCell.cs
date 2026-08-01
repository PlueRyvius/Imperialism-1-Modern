namespace Imperialism.Formats;

/// <summary>
/// Lossless representation of one 36-byte legacy map cell. Unknown fields are
/// intentionally first-class so encoding never discards evidence.
/// </summary>
public sealed record HexCell
{
    public const int Size = 36;

    public byte TerrainUnderlay { get; init; }
    public byte OceanCoastline { get; init; }
    public byte River { get; init; }
    public byte NationZoneA { get; init; }
    public byte NationZoneB { get; init; }
    public byte Unused05 { get; init; } = 255;
    public byte Rail { get; init; }
    public byte NationalBorder { get; init; }
    public byte ProvinceBorder { get; init; }
    public byte LandCoastline { get; init; }
    public byte LikeCellAdjacency { get; init; }
    public byte HillMountainOverlay { get; init; }
    public byte Unused12 { get; init; }
    public byte Unused13 { get; init; }
    public byte Unused14 { get; init; } = 243;
    public byte Unused15 { get; init; }
    public byte Unknown16 { get; init; } = 255;
    public byte ResourceA { get; init; } = 255;
    public byte ResourceB { get; init; } = 255;
    public byte Terrain { get; init; }
    public ushort Province { get; init; }
    public byte Unused22 { get; init; } = 255;
    public byte Unused23 { get; init; }
    public byte Unused24 { get; init; } = 255;
    public byte Unused25 { get; init; } = 243;
    public byte Unused26 { get; init; } = 255;
    public byte Unused27 { get; init; } = 255;
    public byte Unused28 { get; init; }
    public byte TownType { get; init; }
    public byte Unused30 { get; init; } = 243;
    public byte Unused31 { get; init; } = 243;
    public byte Unused32 { get; init; }
    public byte Unused33 { get; init; }
    public byte Unused34 { get; init; }
    public byte Unused35 { get; init; }

    public bool IsOcean => Terrain == 0;

    public static HexCell Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentException($"Expected {Size} bytes, got {bytes.Length}.", nameof(bytes));
        }

        return new HexCell
        {
            TerrainUnderlay = bytes[0],
            OceanCoastline = bytes[1],
            River = bytes[2],
            NationZoneA = bytes[3],
            NationZoneB = bytes[4],
            Unused05 = bytes[5],
            Rail = bytes[6],
            NationalBorder = bytes[7],
            ProvinceBorder = bytes[8],
            LandCoastline = bytes[9],
            LikeCellAdjacency = bytes[10],
            HillMountainOverlay = bytes[11],
            Unused12 = bytes[12],
            Unused13 = bytes[13],
            Unused14 = bytes[14],
            Unused15 = bytes[15],
            Unknown16 = bytes[16],
            ResourceA = bytes[17],
            ResourceB = bytes[18],
            Terrain = bytes[19],
            Province = (ushort)((bytes[20] << 8) | bytes[21]),
            Unused22 = bytes[22],
            Unused23 = bytes[23],
            Unused24 = bytes[24],
            Unused25 = bytes[25],
            Unused26 = bytes[26],
            Unused27 = bytes[27],
            Unused28 = bytes[28],
            TownType = bytes[29],
            Unused30 = bytes[30],
            Unused31 = bytes[31],
            Unused32 = bytes[32],
            Unused33 = bytes[33],
            Unused34 = bytes[34],
            Unused35 = bytes[35],
        };
    }

    public byte[] Encode()
    {
        var bytes = new byte[Size];
        WriteTo(bytes);
        return bytes;
    }

    internal void WriteTo(Span<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentException($"Expected {Size} bytes, got {bytes.Length}.", nameof(bytes));
        }

        bytes[0] = TerrainUnderlay;
        bytes[1] = OceanCoastline;
        bytes[2] = River;
        bytes[3] = NationZoneA;
        bytes[4] = NationZoneB;
        bytes[5] = Unused05;
        bytes[6] = Rail;
        bytes[7] = NationalBorder;
        bytes[8] = ProvinceBorder;
        bytes[9] = LandCoastline;
        bytes[10] = LikeCellAdjacency;
        bytes[11] = HillMountainOverlay;
        bytes[12] = Unused12;
        bytes[13] = Unused13;
        bytes[14] = Unused14;
        bytes[15] = Unused15;
        bytes[16] = Unknown16;
        bytes[17] = ResourceA;
        bytes[18] = ResourceB;
        bytes[19] = Terrain;
        bytes[20] = (byte)(Province >> 8);
        bytes[21] = (byte)Province;
        bytes[22] = Unused22;
        bytes[23] = Unused23;
        bytes[24] = Unused24;
        bytes[25] = Unused25;
        bytes[26] = Unused26;
        bytes[27] = Unused27;
        bytes[28] = Unused28;
        bytes[29] = TownType;
        bytes[30] = Unused30;
        bytes[31] = Unused31;
        bytes[32] = Unused32;
        bytes[33] = Unused33;
        bytes[34] = Unused34;
        bytes[35] = Unused35;
    }
}
