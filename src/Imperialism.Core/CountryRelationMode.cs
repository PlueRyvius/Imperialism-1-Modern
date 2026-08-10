namespace Imperialism.Core;

/// <summary>
/// Original country-manager relation modes that are relevant to strategic sea
/// access. The executable initializes every pair to <see cref="Standard"/>,
/// and its port predicate recognizes only <see cref="Hostile"/>.
/// </summary>
public enum CountryRelationMode : short
{
    Standard = 4,
    Hostile = 6,
}
