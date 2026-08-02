namespace Imperialism.Core;

public enum CellRegionKind : byte
{
    Unassigned,
    Province,
    SeaZone,
}

public readonly record struct CellRegion
{
    private CellRegion(CellRegionKind kind, int value)
    {
        Kind = kind;
        Value = value;
    }

    public static CellRegion Unassigned => default;

    public CellRegionKind Kind { get; }

    internal int Value { get; }

    public static CellRegion ForProvince(ProvinceId province) =>
        new(CellRegionKind.Province, province.Value);

    public static CellRegion ForSeaZone(SeaZoneId seaZone) =>
        new(CellRegionKind.SeaZone, seaZone.Value);

    public ProvinceId Province => Kind == CellRegionKind.Province
        ? new ProvinceId(Value)
        : throw new InvalidOperationException("The cell does not belong to a province.");

    public SeaZoneId SeaZone => Kind == CellRegionKind.SeaZone
        ? new SeaZoneId(Value)
        : throw new InvalidOperationException("The cell does not belong to a sea zone.");
}
