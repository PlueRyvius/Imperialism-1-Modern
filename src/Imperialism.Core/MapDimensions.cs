namespace Imperialism.Core;

public readonly record struct MapDimensions
{
    public MapDimensions(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Map width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Map height must be positive.");
        }

        Width = width;
        Height = height;
        CellCount = checked(width * height);
    }

    public int Width { get; }

    public int Height { get; }

    public int CellCount { get; }

    public bool Contains(HexCoord coordinate) =>
        (uint)coordinate.Column < (uint)Width && (uint)coordinate.Row < (uint)Height;

    public bool Contains(CellIndex index) => index.Value < CellCount;

    public CellIndex GetIndex(HexCoord coordinate)
    {
        if (!Contains(coordinate))
        {
            throw new ArgumentOutOfRangeException(
                nameof(coordinate),
                $"Coordinate {coordinate} is outside the {Width}x{Height} map.");
        }

        return new CellIndex(checked((coordinate.Row * Width) + coordinate.Column));
    }

    public HexCoord GetCoordinate(CellIndex index)
    {
        if (!Contains(index))
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Cell index {index.Value} is outside the {Width}x{Height} map.");
        }

        return new HexCoord(index.Value % Width, index.Value / Width);
    }
}
