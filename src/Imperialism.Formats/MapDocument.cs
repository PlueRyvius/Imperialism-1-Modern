namespace Imperialism.Formats;

/// <summary>A decoded legacy map whose dimensions come from its import profile.</summary>
public sealed class MapDocument
{
    private readonly HexCell[] _cells;
    private readonly byte[] _trailerBytes;

    public MapDocument(
        MapFormatProfile profile,
        IEnumerable<HexCell> cells,
        ReadOnlySpan<byte> trailerBytes)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(cells);

        _cells = cells.ToArray();
        if (_cells.Length != profile.CellCount)
        {
            throw new ArgumentException(
                $"Expected {profile.CellCount} cells, got {_cells.Length}.", nameof(cells));
        }

        if (_cells.Any(static cell => cell is null))
        {
            throw new ArgumentException("Cells cannot contain null values.", nameof(cells));
        }

        if (trailerBytes.Length != profile.TrailerSize)
        {
            throw new ArgumentException(
                $"Expected {profile.TrailerSize} trailer bytes, got {trailerBytes.Length}.",
                nameof(trailerBytes));
        }

        Profile = profile;
        _trailerBytes = trailerBytes.ToArray();
    }

    public MapFormatProfile Profile { get; }

    public int Width => Profile.Width;

    public int Height => Profile.Height;

    public IReadOnlyList<HexCell> Cells => _cells;

    public ReadOnlyMemory<byte> TrailerBytes => _trailerBytes;

    public HexCell this[int x, int y]
    {
        get => _cells[GetIndex(x, y)];
        set => _cells[GetIndex(x, y)] = value ?? throw new ArgumentNullException(nameof(value));
    }

    public int GetIndex(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x), $"Coordinate ({x}, {y}) is outside {Width}x{Height}.");
        }

        return checked((y * Width) + x);
    }

    public static MapDocument CreateBlank(MapFormatProfile? profile = null)
    {
        profile ??= MapFormatProfile.Imperialism1;
        var blankCell = new HexCell
        {
            TerrainUnderlay = 5,
            Terrain = 0,
            Province = ushort.MaxValue,
        };
        return new MapDocument(
            profile,
            Enumerable.Repeat(blankCell, profile.CellCount),
            new byte[profile.TrailerSize]);
    }
}
