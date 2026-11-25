namespace Imperialism.Core;

/// <summary>
/// Records a country eliminated (row 6e): its capital captured, or a capture that left it
/// no province. Its remaining provinces fall into anarchy and keep their owner.
/// </summary>
public sealed record CountryEliminatedEvent : TurnEvent
{
    public CountryEliminatedEvent(
        int turnNumber,
        CountryId country,
        CountryId by,
        int provincesInAnarchy,
        int fleetsLost,
        long shipsLost,
        int diplomaticTiesClosed,
        int tradePoliciesCleared)
        : base(turnNumber, TurnPhase.Conflict)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tradePoliciesCleared);
        ArgumentOutOfRangeException.ThrowIfNegative(provincesInAnarchy);
        ArgumentOutOfRangeException.ThrowIfNegative(fleetsLost);
        ArgumentOutOfRangeException.ThrowIfNegative(shipsLost);
        ArgumentOutOfRangeException.ThrowIfNegative(diplomaticTiesClosed);
        if (country == by)
        {
            throw new ArgumentException("A country is eliminated by someone else.", nameof(by));
        }

        Country = country;
        By = by;
        ProvincesInAnarchy = provincesInAnarchy;
        FleetsLost = fleetsLost;
        ShipsLost = shipsLost;
        DiplomaticTiesClosed = diplomaticTiesClosed;
        TradePoliciesCleared = tradePoliciesCleared;
    }

    public CountryId Country { get; }

    /// <summary>The country whose capture eliminated it.</summary>
    public CountryId By { get; }

    /// <summary>The provinces it still owned, every one now in anarchy.</summary>
    public int ProvincesInAnarchy { get; }

    /// <summary>Fleet records removed, assembled or not.</summary>
    public int FleetsLost { get; }

    /// <summary>
    /// Every hull it owned, fleet records and the abstract fair-start merchants alike.
    /// </summary>
    public long ShipsLost { get; }

    /// <summary>Consulates, embassies and pacts closed, counted per (holder, host, tie).</summary>
    public int DiplomaticTiesClosed { get; }

    /// <summary>Subsidies and boycotts cleared, either way.</summary>
    public int TradePoliciesCleared { get; }
}

/// <summary>
/// Records that a country eliminated on an earlier turn submitted orders, and that they
/// were set aside before anything resolved. Emitted only when the orders were not empty.
/// </summary>
public sealed record EliminatedCountryOrdersIgnoredEvent : TurnEvent
{
    public EliminatedCountryOrdersIgnoredEvent(int turnNumber, CountryId country)
        : base(turnNumber, TurnPhase.Diplomacy)
    {
        Country = country;
    }

    public CountryId Country { get; }
}

/// <summary>
/// Row 6e: elimination and anarchy. A country is eliminated when its capital is captured,
/// or when a capture leaves it no province; from then on its provinces are in anarchy.
/// </summary>
/// <remarks>
/// <b>Permanent, and ownership is kept.</b> An anarchic province still reads as its old
/// owner's — <see cref="WorldState.IsInAnarchy"/> is exactly "the owner is eliminated" —
/// and anyone may attack it without a war. Every rule here the manual does not state is
/// labelled in <c>docs/formulas/army.md</c>, rules 38 onwards.
/// </remarks>
internal static class EliminationPlanner
{
    /// <summary>
    /// Replaces the orders of every country eliminated on an earlier turn with empty ones,
    /// reporting each that was not already empty. Runs before anything is planned.
    /// </summary>
    public static TurnOrders IgnoreEliminated(
        WorldState state, TurnOrders orders, int turnNumber, List<TurnEvent> events)
    {
        var replaced = false;
        var result = new CountryTurnOrders[orders.Count];
        for (var index = 0; index < orders.Count; index++)
        {
            var country = new CountryId(index);
            var given = orders[country];
            if (state.IsEliminated(country))
            {
                if (!given.IsEmpty)
                {
                    events.Add(new EliminatedCountryOrdersIgnoredEvent(turnNumber, country));
                }

                result[index] = new CountryTurnOrders(country);
                replaced = true;
                continue;
            }

            result[index] = given;
        }

        return replaced ? new TurnOrders(result) : orders;
    }

    /// <summary>
    /// After a capture: eliminates the previous owner if it lost its capital, or if it now
    /// owns no province. A country that has already lost its capital but still holds land
    /// is not eliminated by losing another province (chosen, army rule 39).
    /// </summary>
    public static CountryEliminatedEvent? AfterCapture(
        WorldState state, CountryId previousOwner, CountryId by, bool capitalLost, int turnNumber)
    {
        if (state.IsEliminated(previousOwner))
        {
            return null;
        }

        var provinces = OwnedProvinceCount(state, previousOwner);
        return capitalLost || provinces == 0
            ? Eliminate(state, previousOwner, by, provinces, turnNumber)
            : null;
    }

    private static CountryEliminatedEvent Eliminate(
        WorldState state, CountryId country, CountryId by, int provinces, int turnNumber)
    {
        _ = state.MarkEliminated(country, turnNumber);
        var definition = state.Definition;

        // Every hull, fleet records and abstract merchants alike, counted before removal.
        var shipsLost = 0L;
        for (var type = 0; type < definition.ShipTypes.Count; type++)
        {
            shipsLost = checked(shipsLost + state.GetShipCount(country, new ShipTypeId(type)));
        }

        // Fleets go down with the state (chosen, army rule 40). Sinking every ship of a
        // fleet removes it and dissolves an emptied task force, landing sites included.
        var fleets = state.Fleets.Where(fleet => fleet.Country == country).ToArray();
        foreach (var fleet in fleets)
        {
            state.SinkShips(fleet.Id, fleet.Count);
        }

        for (var type = 0; type < definition.ShipTypes.Count; type++)
        {
            state.SetShipCount(country, new ShipTypeId(type), 0);
        }

        // Every post and pact, both ways, and every trade policy, both ways.
        var ties = 0;
        var policies = 0;
        for (var index = 0; index < definition.Countries.Count; index++)
        {
            var other = new CountryId(index);
            if (other == country)
            {
                continue;
            }

            foreach (var tie in new[] { DiplomaticTie.Consulate, DiplomaticTie.Embassy, DiplomaticTie.Pact })
            {
                ties += state.RemoveDiplomaticTie(country, other, tie) ? 1 : 0;
                ties += state.RemoveDiplomaticTie(other, country, tie) ? 1 : 0;
            }

            foreach (var (owner, host) in new[] { (country, other), (other, country) })
            {
                if (state.GetTradePolicy(owner, host).Kind != TradePolicyKind.None)
                {
                    state.SetTradePolicy(owner, host, TradePolicy.None);
                    policies++;
                }
            }
        }

        return new CountryEliminatedEvent(
            turnNumber, country, by, provinces, fleets.Length, shipsLost, ties, policies);
    }

    private static int OwnedProvinceCount(WorldState state, CountryId country)
    {
        var count = 0;
        for (var province = 0; province < state.Definition.Map.Provinces.Count; province++)
        {
            if (state.GetProvinceOwner(new ProvinceId(province)) == country)
            {
                count++;
            }
        }

        return count;
    }
}
