using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

public sealed record WorldCellView(
    CellIndex Index,
    string? OwnerKey,
    string? OwnerName,
    CountryId? CapitalCountry);

public sealed class WorldViewState
{
    private readonly IReadOnlyList<WorldCellView> _cells;
    private readonly IReadOnlyList<CellLink> _rails;

    private WorldViewState(
        string mapKey,
        string scenarioKey,
        string scenarioName,
        TurnDate currentDate,
        IEnumerable<WorldCellView> cells,
        IEnumerable<CellLink> rails)
    {
        MapKey = mapKey;
        ScenarioKey = scenarioKey;
        ScenarioName = scenarioName;
        CurrentDate = currentDate;
        _cells = Array.AsReadOnly(cells.ToArray());
        _rails = Array.AsReadOnly(rails.ToArray());
    }

    public string MapKey { get; }

    public string ScenarioKey { get; }

    public string ScenarioName { get; }

    public TurnDate CurrentDate { get; }

    public int CurrentYear => CurrentDate.Year;

    public IReadOnlyList<WorldCellView> Cells => _cells;

    public IReadOnlyList<CellLink> Rails => _rails;

    public WorldCellView this[CellIndex index] => (uint)index.Value < (uint)_cells.Count
        ? _cells[index.Value]
        : throw new ArgumentOutOfRangeException(nameof(index));

    public static WorldViewState Create(
        CompiledWorldPackage package,
        string scenarioKey,
        WorldState state)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        ArgumentNullException.ThrowIfNull(state);
        var world = package.GetWorld(scenarioKey);
        if (!ReferenceEquals(world, state.Definition))
        {
            throw new ArgumentException(
                "The runtime state must belong to the selected package scenario.",
                nameof(state));
        }

        var capitalCountries = new Dictionary<CellIndex, CountryId>();
        foreach (var country in world.Countries)
        {
            var capital = state.GetCountryCapital(country.Id);
            if (capital.HasValue)
            {
                capitalCountries.Add(capital.Value, country.Id);
            }
        }

        var cells = new WorldCellView[world.Map.Cells.Count];
        foreach (var cell in world.Map.Cells)
        {
            CountryId? owner = cell.Region.Kind == CellRegionKind.Province
                ? state.GetProvinceOwner(cell.Region.Province)
                : null;
            cells[cell.Index.Value] = new WorldCellView(
                cell.Index,
                owner.HasValue ? package.Catalog.GetKey(owner.Value) : null,
                owner.HasValue ? world.Countries[owner.Value.Value].Name : null,
                capitalCountries.TryGetValue(cell.Index, out var capitalCountry)
                    ? capitalCountry
                    : null);
        }

        return new WorldViewState(
            package.MapKey,
            scenarioKey,
            world.Scenario.Name,
            state.CurrentDate,
            cells,
            state.GetRailLinks());
    }
}
