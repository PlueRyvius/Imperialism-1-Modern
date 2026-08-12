namespace Imperialism.Core;

public sealed class ScenarioDefinition
{
    private readonly IReadOnlyList<CountryId?> _initialProvinceOwners;
    private readonly IReadOnlyList<CellLink> _initialRailLinks;
    private readonly IReadOnlyList<CountryCapital> _initialCountryCapitals;
    private readonly IReadOnlyList<InitialCommodityStock> _initialInventory;
    private readonly IReadOnlyList<InitialProductionCapacity> _initialProductionCapacities;
    private readonly IReadOnlyList<InitialCellDevelopment> _initialCellDevelopment;
    private readonly IReadOnlyList<InitialCountryTechnology> _initialCountryTechnologies;
    private readonly IReadOnlyList<CellIndex> _initialPorts;
    private readonly IReadOnlyList<CellIndex> _initialDepots;
    private readonly IReadOnlyList<InitialWorkforce> _initialWorkforce;
    private readonly IReadOnlyList<InitialCivilian> _initialCivilians;
    private readonly IReadOnlyList<CountryId> _defaultStartCountries;
    private readonly IReadOnlyList<InitialTransportCapacity> _initialTransportCapacity;
    private readonly IReadOnlyList<InitialCash> _initialCash;
    private readonly IReadOnlyList<InitialShip> _initialShips;
    private readonly IReadOnlyList<InitialArmy> _initialArmies;
    private readonly IReadOnlyList<InitialRelation> _initialRelations;
    private readonly IReadOnlyList<InitialRelationState> _initialRelationStates;

    public ScenarioDefinition(
        string name,
        int startingYear,
        IEnumerable<CountryId?> initialProvinceOwners,
        IEnumerable<CellLink>? initialRailLinks = null,
        IEnumerable<CountryCapital>? initialCountryCapitals = null,
        IEnumerable<InitialCommodityStock>? initialInventory = null,
        IEnumerable<InitialProductionCapacity>? initialProductionCapacities = null,
        IEnumerable<InitialCellDevelopment>? initialCellDevelopment = null,
        IEnumerable<InitialCountryTechnology>? initialCountryTechnologies = null,
        IEnumerable<CellIndex>? initialPorts = null,
        IEnumerable<CellIndex>? initialDepots = null,
        IEnumerable<InitialWorkforce>? initialWorkforce = null,
        IEnumerable<CountryId>? defaultStartCountries = null,
        IEnumerable<InitialCivilian>? initialCivilians = null,
        IEnumerable<InitialTransportCapacity>? initialTransportCapacity = null,
        IEnumerable<InitialCash>? initialCash = null,
        IEnumerable<InitialShip>? initialShips = null,
        IEnumerable<InitialRelation>? initialRelations = null,
        IEnumerable<InitialRelationState>? initialRelationStates = null,
        short initialRelationSequence = 0,
        IEnumerable<InitialArmy>? initialArmies = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(initialProvinceOwners);
        var railArray = initialRailLinks?.ToArray() ?? [];
        var capitalArray = initialCountryCapitals?.ToArray() ?? [];
        var inventoryArray = initialInventory?.ToArray() ?? [];
        var capacityArray = initialProductionCapacities?.ToArray() ?? [];
        var developmentArray = initialCellDevelopment?.ToArray() ?? [];
        var technologyArray = initialCountryTechnologies?.ToArray() ?? [];
        var portArray = initialPorts?.ToArray() ?? [];
        if (portArray.Distinct().Count() != portArray.Length)
        {
            throw new ArgumentException("Initial ports cannot contain duplicates.", nameof(initialPorts));
        }

        var depotArray = initialDepots?.ToArray() ?? [];
        if (depotArray.Distinct().Count() != depotArray.Length)
        {
            throw new ArgumentException("Initial depots cannot contain duplicates.", nameof(initialDepots));
        }

        var workforceArray = initialWorkforce?.ToArray() ?? [];
        if (workforceArray.Select(static item => item.Country).Distinct().Count() != workforceArray.Length)
        {
            throw new ArgumentException(
                "A country cannot have more than one initial workforce.",
                nameof(initialWorkforce));
        }

        var transportArray = initialTransportCapacity?.ToArray() ?? [];
        if (transportArray.Select(static item => item.Country).Distinct().Count() != transportArray.Length)
        {
            throw new ArgumentException(
                "A country cannot have more than one initial transport capacity.",
                nameof(initialTransportCapacity));
        }

        if (transportArray.Any(static item => item.Capacity < 0))
        {
            throw new ArgumentException(
                "Initial transport capacity cannot be negative.",
                nameof(initialTransportCapacity));
        }

        var cashArray = initialCash?.ToArray() ?? [];
        if (cashArray.Select(static item => item.Country).Distinct().Count() != cashArray.Length)
        {
            throw new ArgumentException(
                "A country cannot have more than one initial treasury.",
                nameof(initialCash));
        }

        if (developmentArray.Select(static item => item.Cell).Distinct().Count() != developmentArray.Length)
        {
            throw new ArgumentException(
                "A cell cannot have more than one initial development level.",
                nameof(initialCellDevelopment));
        }

        if (technologyArray.Distinct().Count() != technologyArray.Length)
        {
            throw new ArgumentException(
                "Initial technologies cannot repeat a country and technology pair.",
                nameof(initialCountryTechnologies));
        }

        if (capacityArray.Any(static item => item.Quantity <= 0))
        {
            throw new ArgumentException(
                "Initial production capacity quantities must be positive.",
                nameof(initialProductionCapacities));
        }
        if (railArray.Length != railArray.Distinct().Count())
        {
            throw new ArgumentException("Initial rail links cannot contain duplicates.", nameof(initialRailLinks));
        }

        if (capitalArray.Select(static capital => capital.Country).Distinct().Count() != capitalArray.Length)
        {
            throw new ArgumentException(
                "A country cannot have more than one initial capital.",
                nameof(initialCountryCapitals));
        }

        if (capitalArray.Select(static capital => capital.Cell).Distinct().Count() != capitalArray.Length)
        {
            throw new ArgumentException(
                "A cell cannot be the initial capital of more than one country.",
                nameof(initialCountryCapitals));
        }

        if (inventoryArray.Select(static stock => (stock.Country, stock.Commodity)).Distinct().Count() !=
            inventoryArray.Length)
        {
            throw new ArgumentException(
                "Initial inventory cannot contain duplicate country and commodity entries.",
                nameof(initialInventory));
        }

        if (capacityArray.Select(static item => (item.Country, item.Facility)).Distinct().Count() !=
            capacityArray.Length)
        {
            throw new ArgumentException(
                "Initial production capacities cannot contain duplicate country and facility entries.",
                nameof(initialProductionCapacities));
        }

        Name = name;
        StartingYear = startingYear;
        _initialProvinceOwners = Array.AsReadOnly(initialProvinceOwners.ToArray());
        _initialRailLinks = Array.AsReadOnly(railArray);
        _initialCountryCapitals = Array.AsReadOnly(capitalArray);
        _initialInventory = Array.AsReadOnly(inventoryArray);
        _initialProductionCapacities = Array.AsReadOnly(capacityArray);
        _initialCellDevelopment = Array.AsReadOnly(developmentArray);
        _initialCountryTechnologies = Array.AsReadOnly(technologyArray);
        _initialPorts = Array.AsReadOnly(portArray);
        _initialDepots = Array.AsReadOnly(depotArray);
        _initialWorkforce = Array.AsReadOnly(workforceArray);
        _initialTransportCapacity = Array.AsReadOnly(transportArray);
        _initialCash = Array.AsReadOnly(cashArray);

        // Not made unique by (country, type, zone). The corpus repeats the
        // combination freely -- s1 gives one power `8x2 8x1` and s13 gives another
        // `3x2 3x1 3x1` -- so a fleet is a bag of records rather than a table, and
        // erroring on a repeat would reject shipped data. They sum.
        _initialShips = Array.AsReadOnly(initialShips?.ToArray() ?? []);

        // The source is a bag of records, not a province/type table. Retain its
        // order and repeats until the original stacking and battle-selection
        // rules are recovered.
        _initialArmies = Array.AsReadOnly(initialArmies?.ToArray() ?? []);

        // The EXE applies relationship records in scenario order and mirrors each
        // value into both halves of its matrix. Keep the source records as an ordered
        // sequence; later diplomacy work can replay that exact behavior instead of
        // silently choosing a duplicate policy here.
        _initialRelations = Array.AsReadOnly(initialRelations?.ToArray() ?? []);

        // Like the raw records, preserve state records in source order. The
        // original setter mirrors a pair, so later entries may intentionally
        // replace an earlier pair in a saved or diagnostic snapshot.
        _initialRelationStates = Array.AsReadOnly(initialRelationStates?.ToArray() ?? []);
        InitialRelationSequence = initialRelationSequence;

        // Civilians are deliberately not made unique by cell. The original
        // stacks them freely — `s1` gives one power two Miners — and nothing in
        // the manual says a tile holds only one.
        _initialCivilians = Array.AsReadOnly(initialCivilians?.ToArray() ?? []);

        var defaultStartArray = defaultStartCountries?.ToArray() ?? [];
        if (defaultStartArray.Distinct().Count() != defaultStartArray.Length)
        {
            throw new ArgumentException(
                "Default-start countries cannot contain duplicates.",
                nameof(defaultStartCountries));
        }

        _defaultStartCountries = Array.AsReadOnly(defaultStartArray);
    }

    public string Name { get; }

    public int StartingYear { get; }

    public IReadOnlyList<CountryId?> InitialProvinceOwners => _initialProvinceOwners;

    public IReadOnlyList<CellLink> InitialRailLinks => _initialRailLinks;

    public IReadOnlyList<CountryCapital> InitialCountryCapitals => _initialCountryCapitals;

    public IReadOnlyList<InitialCommodityStock> InitialInventory => _initialInventory;

    public IReadOnlyList<InitialProductionCapacity> InitialProductionCapacities =>
        _initialProductionCapacities;

    public IReadOnlyList<InitialCellDevelopment> InitialCellDevelopment => _initialCellDevelopment;

    public IReadOnlyList<InitialCountryTechnology> InitialCountryTechnologies =>
        _initialCountryTechnologies;

    public IReadOnlyList<CellIndex> InitialPorts => _initialPorts;

    public IReadOnlyList<CellIndex> InitialDepots => _initialDepots;

    public IReadOnlyList<InitialWorkforce> InitialWorkforce => _initialWorkforce;

    /// <summary>
    /// What each country's network can carry at the start. The 1997 <c>tran</c>
    /// record, which a mission authors per power and a skirmish leaves to the
    /// engine.
    /// </summary>
    public IReadOnlyList<InitialTransportCapacity> InitialTransportCapacity =>
        _initialTransportCapacity;

    /// <summary>
    /// What each country's treasury holds at the start: the 1997 <c>cash</c>
    /// record, which a mission authors per power and a skirmish leaves to the
    /// engine.
    /// </summary>
    public IReadOnlyList<InitialCash> InitialCash => _initialCash;

    /// <summary>
    /// The fleets each country starts with: the 1997 <c>ship</c> record.
    /// </summary>
    /// <remarks>
    /// <b>Unlike the seven engine defaults, a skirmish authors these</b> — `s10`, `s11`
    /// and `s15` carry no <c>ware</c>, <c>cash</c>, <c>tech</c> or <c>tran</c> and all
    /// three give every power three ships apiece. So the opening merchant marine is
    /// recoverable from the corpus rather than being another unrecoverable constant,
    /// on the same skirmish-agreement argument that settled the workforce and the mills.
    /// </remarks>
    public IReadOnlyList<InitialShip> InitialShips => _initialShips;

    /// <summary>
    /// Army stacks the scenario starts with: the original <c>army</c> records,
    /// preserved in source order for a later tactical-battle setup pass.
    /// </summary>
    public IReadOnlyList<InitialArmy> InitialArmies => _initialArmies;

    /// <summary>
    /// Raw relationship records the scenario starts with: the 1997 <c>rela</c>
    /// records. They are retained separately from the active relation state
    /// used by strategic port access.
    /// </summary>
    public IReadOnlyList<InitialRelation> InitialRelations => _initialRelations;

    /// <summary>
    /// Active relation mode/token entries at the start of the scenario.
    /// Omitted entries use the original defaults: standard mode and token -1.
    /// </summary>
    public IReadOnlyList<InitialRelationState> InitialRelationStates => _initialRelationStates;

    /// <summary>
    /// Active relation generation at the start of the scenario. This is needed
    /// with <see cref="InitialRelationStates"/> to preserve whether a hostile
    /// mode is immediately effective.
    /// </summary>
    public short InitialRelationSequence { get; }

    /// <summary>
    /// Civilians on the map at the start, in the order they will be issued ids.
    /// </summary>
    public IReadOnlyList<InitialCivilian> InitialCivilians => _initialCivilians;

    /// <summary>
    /// Countries that begin from the world's <see cref="StartingDefaults"/>: a
    /// fair start, the same for each of them.
    /// </summary>
    /// <remarks>
    /// Named rather than inferred. The original equips its seven Great Powers
    /// and leaves the minor nations without an industry screen at all, and Core
    /// has no notion of which a country is — applying defaults to everyone would
    /// hand a workforce to every statelet on the map. An explicit entry for a
    /// listed country still wins over the default.
    /// </remarks>
    public IReadOnlyList<CountryId> DefaultStartCountries => _defaultStartCountries;
}
