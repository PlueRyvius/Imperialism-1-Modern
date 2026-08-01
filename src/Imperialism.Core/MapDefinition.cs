namespace Imperialism.Core;

public sealed class MapDefinition
{
    private readonly IReadOnlyList<CellDefinition> _cells;
    private readonly IReadOnlyList<ProvinceDefinition> _provinces;
    private readonly IReadOnlyList<SeaZoneDefinition> _seaZones;
    private readonly IReadOnlyList<CellLink> _rivers;
    private readonly HashSet<CellLink> _riverSet;
    private readonly IReadOnlyList<CellIndex>[] _provinceCells;
    private readonly IReadOnlyList<CellIndex>[] _seaZoneCells;

    public MapDefinition(
        MapDimensions dimensions,
        IEnumerable<CellDefinition> cells,
        IEnumerable<ProvinceDefinition>? provinces = null,
        IEnumerable<SeaZoneDefinition>? seaZones = null,
        IEnumerable<CellLink>? rivers = null)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var cellArray = cells.ToArray();
        var provinceArray = provinces?.ToArray() ?? [];
        var seaZoneArray = seaZones?.ToArray() ?? [];
        var riverArray = rivers?.ToArray() ?? [];
        if (cellArray.Any(static cell => cell is null))
        {
            throw new ArgumentException("Cells cannot contain null entries.", nameof(cells));
        }

        if (provinceArray.Any(static province => province is null))
        {
            throw new ArgumentException("Provinces cannot contain null entries.", nameof(provinces));
        }

        if (seaZoneArray.Any(static seaZone => seaZone is null))
        {
            throw new ArgumentException("Sea zones cannot contain null entries.", nameof(seaZones));
        }

        if (cellArray.Length != dimensions.CellCount)
        {
            throw new ArgumentException(
                $"Expected {dimensions.CellCount} cells, got {cellArray.Length}.",
                nameof(cells));
        }

        ValidateDenseIds(provinceArray.Select(static province => province.Id.Value), "province");
        ValidateDenseIds(seaZoneArray.Select(static seaZone => seaZone.Id.Value), "sea zone");
        ValidateLinks(riverArray, dimensions, "River", nameof(rivers));

        var provinceCells = CreateMembershipLists(provinceArray.Length);
        var seaZoneCells = CreateMembershipLists(seaZoneArray.Length);
        for (var index = 0; index < cellArray.Length; index++)
        {
            var cell = cellArray[index];
            var expectedIndex = new CellIndex(index);
            var expectedCoordinate = dimensions.GetCoordinate(expectedIndex);
            if (cell.Index != expectedIndex || cell.Coordinate != expectedCoordinate)
            {
                throw new ArgumentException(
                    $"Cell {index} must have index {expectedIndex} and coordinate {expectedCoordinate}.",
                    nameof(cells));
            }

            switch (cell.Region.Kind)
            {
                case CellRegionKind.Unassigned:
                    break;
                case CellRegionKind.Province:
                    if ((uint)cell.Region.Value >= (uint)provinceCells.Length)
                    {
                        throw new ArgumentException(
                            $"Cell {index} refers to missing province {cell.Region.Value}.",
                            nameof(cells));
                    }

                    provinceCells[cell.Region.Value].Add(expectedIndex);
                    break;
                case CellRegionKind.SeaZone:
                    if ((uint)cell.Region.Value >= (uint)seaZoneCells.Length)
                    {
                        throw new ArgumentException(
                            $"Cell {index} refers to missing sea zone {cell.Region.Value}.",
                            nameof(cells));
                    }

                    seaZoneCells[cell.Region.Value].Add(expectedIndex);
                    break;
                default:
                    throw new ArgumentException(
                        $"Cell {index} has unknown region kind {cell.Region.Kind}.",
                        nameof(cells));
            }
        }

        Dimensions = dimensions;
        _cells = Array.AsReadOnly(cellArray);
        _provinces = Array.AsReadOnly(provinceArray);
        _seaZones = Array.AsReadOnly(seaZoneArray);
        _rivers = Array.AsReadOnly(SortLinks(riverArray));
        _riverSet = riverArray.ToHashSet();
        _provinceCells = FreezeMembershipLists(provinceCells);
        _seaZoneCells = FreezeMembershipLists(seaZoneCells);
    }

    public MapDimensions Dimensions { get; }

    public IReadOnlyList<CellDefinition> Cells => _cells;

    public IReadOnlyList<ProvinceDefinition> Provinces => _provinces;

    public IReadOnlyList<SeaZoneDefinition> SeaZones => _seaZones;

    public IReadOnlyList<CellLink> Rivers => _rivers;

    public CellDefinition this[CellIndex index] => Dimensions.Contains(index)
        ? _cells[index.Value]
        : throw new ArgumentOutOfRangeException(nameof(index));

    public CellDefinition this[HexCoord coordinate] => this[Dimensions.GetIndex(coordinate)];

    public IReadOnlyList<CellIndex> GetCells(ProvinceId province) =>
        (uint)province.Value < (uint)_provinceCells.Length
            ? _provinceCells[province.Value]
            : throw new ArgumentOutOfRangeException(nameof(province));

    public IReadOnlyList<CellIndex> GetCells(SeaZoneId seaZone) =>
        (uint)seaZone.Value < (uint)_seaZoneCells.Length
            ? _seaZoneCells[seaZone.Value]
            : throw new ArgumentOutOfRangeException(nameof(seaZone));

    public bool HasRiver(CellLink link) => _riverSet.Contains(link);

    private static List<CellIndex>[] CreateMembershipLists(int count) =>
        Enumerable.Range(0, count).Select(static _ => new List<CellIndex>()).ToArray();

    private static IReadOnlyList<CellIndex>[] FreezeMembershipLists(List<CellIndex>[] memberships) =>
        memberships.Select(static cells => (IReadOnlyList<CellIndex>)cells.AsReadOnly()).ToArray();

    private static void ValidateDenseIds(IEnumerable<int> ids, string description)
    {
        var values = ids.ToArray();
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] != index)
            {
                throw new ArgumentException(
                    $"Modern {description} IDs must be dense and ordered; expected {index}, got {values[index]}.");
            }
        }
    }

    internal static void ValidateLinks(
        IReadOnlyCollection<CellLink> links,
        MapDimensions dimensions,
        string description,
        string parameterName)
    {
        if (links.Count != links.Distinct().Count())
        {
            throw new ArgumentException($"{description} links cannot contain duplicates.", parameterName);
        }

        foreach (var link in links)
        {
            try
            {
                link.Validate(dimensions, description);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(exception.Message, parameterName, exception);
            }
        }
    }

    private static CellLink[] SortLinks(IEnumerable<CellLink> links) =>
        links.OrderBy(static link => link.First.Value)
            .ThenBy(static link => link.Second.Value)
            .ToArray();
}
