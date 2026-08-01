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
