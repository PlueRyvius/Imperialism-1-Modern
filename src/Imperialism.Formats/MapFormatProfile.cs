namespace Imperialism.Formats;

/// <summary>Physical layout required to decode a headerless legacy map.</summary>
public sealed record MapFormatProfile
{
    public static MapFormatProfile Imperialism1 { get; } = new(108, 60, 384, 198);

    public MapFormatProfile(
        int width,
        int height,
        int trailerRecordCount = 384,
        int trailerRecordSize = 198)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        if (trailerRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trailerRecordCount), "Trailer record count cannot be negative.");
        }

        if (trailerRecordSize < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trailerRecordSize), "Trailer record size cannot be negative.");
        }

        if (trailerRecordCount > 0 && trailerRecordSize == 0)
        {
            throw new ArgumentException(
                "Non-empty trailer records must have a size.", nameof(trailerRecordSize));
        }

        Width = width;
        Height = height;
        TrailerRecordCount = trailerRecordCount;
        TrailerRecordSize = trailerRecordSize;

        _ = FileSize;
    }

    public int Width { get; }

    public int Height { get; }

    public int TrailerRecordCount { get; }

    public int TrailerRecordSize { get; }

    public int CellCount => checked(Width * Height);

    public int TrailerSize => checked(TrailerRecordCount * TrailerRecordSize);

    public int FileSize => checked(checked(CellCount * HexCell.Size) + TrailerSize);
}
