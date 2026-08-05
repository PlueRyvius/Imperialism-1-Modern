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
/// </remarks>
public sealed record TerrainDefinition
{
    public TerrainDefinition(TerrainId id, string name, bool isImprovable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
        IsImprovable = isImprovable;
    }

    public TerrainId Id { get; }

    public string Name { get; }

    /// <summary>
    /// Whether a civilian can raise this cell's development level at all. False
    /// for water, for settlements, and for the manual's three barren cases.
    /// </summary>
    public bool IsImprovable { get; }
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
