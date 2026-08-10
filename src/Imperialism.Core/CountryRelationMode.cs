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

/// <summary>
/// One authored initial entry in the country manager's active relation tables.
/// Unlike <see cref="InitialRelation"/>, this is the mode and effective-token
/// state consumed by strategic port access, not a raw 1997 <c>rela</c> score.
/// </summary>
/// <remarks>
/// Mode values stay raw signed 16-bit values. The original has modes outside
/// the two currently modelled values, and retaining them avoids claiming an
/// interpretation before their behavior is recovered.
/// </remarks>
public readonly record struct InitialRelationState(
    CountryId First,
    CountryId Second,
    short Mode,
    short Token);
