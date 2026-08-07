namespace Imperialism.Core;

public sealed record ProvinceDefinition
{
    public ProvinceDefinition(ProvinceId id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
    }

    public ProvinceId Id { get; }

    public string Name { get; }
}

public sealed record SeaZoneDefinition
{
    public SeaZoneDefinition(SeaZoneId id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
    }

    public SeaZoneId Id { get; }

    public string Name { get; }
}

/// <summary>
/// Terms on which a Prospector may search this terrain for hidden deposits.
/// </summary>
/// <remarks>
/// The presence of this rule is what makes a terrain searchable at all, so
/// "cannot be searched" is a null rule rather than a flag beside a nullable
/// technology. The distinction matters: barren hills are searchable with no
/// technology at all, and a bool-plus-nullable pair would spell that the same
/// way as a farm, which is searchable by nobody ever.
/// <para>
/// The manual's Terrain Tiles Table gives barren hills and mountains "Miner,
/// Prospector" and swamp, desert and tundra "Driller, Prospector", and the
/// Prospector paragraph gates the second group: "when your country invests in
/// Oil Drilling technology, the eye cursor appears over unprospected swamps,
/// deserts, and tundra as well".
/// </para>
/// </remarks>
public sealed record ProspectingRule
{
    /// <summary>Searchable by anyone, from the first turn. Barren hills and mountains.</summary>
    public static readonly ProspectingRule Unrestricted = new();

    public ProspectingRule(TechnologyId? requiredTechnology = null) =>
        RequiredTechnology = requiredTechnology;

    /// <summary>
    /// Knowledge a country needs before it may search this ground at all. Null
    /// means none. Oil Drilling is the manual's one instance, and it gates the
    /// <em>discovery</em> rather than the extraction — which is why no imported
    /// deposit declares a <see cref="ResourceDefinition.RequiredTechnology"/>.
    /// </summary>
    public TechnologyId? RequiredTechnology { get; }
}

/// <summary>
/// A terrain type and what a civilian may do to it. Terrain used to be a bare
/// id with a key string, which is why improvability could not depend on it.
/// </summary>
/// <remarks>
/// The manual's Terrain Tiles Table gives every terrain a civilian worker, and
/// three of them — dry plains, horse ranch and scrub forest — get "None". That
/// is a property of the ground, not of the deposit: grain is improvable on a
/// farm and not on dry plains, timber in hardwood forest and not in scrub. The
/// shipped corpus agrees without exception: of 481 <c>deve</c> records across
/// five scenarios, not one lands on any of the three. See
/// <c>docs/formulas/development.md</c>.
/// <para>
/// Prospecting is the same shape and a different table. Improvability says
/// whether a tile can be made to yield more; prospectability says whether the
/// tile might be hiding anything to yield at all. Barren hills and mountains
/// are both, dry plains neither, and a farm is improvable but holds no secret.
/// </para>
/// </remarks>
public sealed record TerrainDefinition
{
    public TerrainDefinition(
        TerrainId id,
        string name,
        bool isImprovable = false,
        ProspectingRule? prospecting = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
        IsImprovable = isImprovable;
        Prospecting = prospecting;
    }

    public TerrainId Id { get; }

    public string Name { get; }

    /// <summary>
    /// Whether a civilian can raise this cell's development level at all. False
    /// for water, for settlements, and for the manual's three barren cases.
    /// </summary>
    public bool IsImprovable { get; }

    /// <summary>
    /// On what terms a Prospector may search this ground, or null where it can
    /// never be searched — which is every terrain but five.
    /// </summary>
    public ProspectingRule? Prospecting { get; }
}

public sealed record CountryDefinition
{
    public CountryDefinition(CountryId id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
    }

    public CountryId Id { get; }

    public string Name { get; }
}

public readonly record struct CountryCapital(CountryId Country, CellIndex Cell);
