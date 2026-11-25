namespace Imperialism.Core;

/// <summary>A source of battle draws. Separate from the resolver so a test or a trace can replay one.</summary>
internal interface IBattleRandom
{
    ulong Next();
}

/// <summary>
/// SplitMix64: small, self-contained and the same on every platform, so the same turn
/// seed and the same orders fight the same battles. Seeded per battle from the turn's
/// seed and the battle's 0-based ordinal within the turn — a chosen rule.
/// </summary>
internal sealed class SplitMix64 : IBattleRandom
{
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;
    private ulong _state;

    public SplitMix64(ulong state) => _state = state;

    /// <summary>
    /// The stream for one battle: the turn's seed XORed with the ordinal passed through
    /// SplitMix64's own finaliser, so neighbouring battles do not share a shifted stream.
    /// </summary>
    public static SplitMix64 ForBattle(ulong turnSeed, int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        return new SplitMix64(turnSeed ^ Mix(unchecked((ulong)ordinal + 1)));
    }

    public ulong Next()
    {
        unchecked
        {
            _state += Gamma;
            return Mix(_state);
        }
    }

    private static ulong Mix(ulong z)
    {
        unchecked
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}

/// <summary>One regiment as a battle sees it.</summary>
internal readonly record struct BattleCombatant(
    RegimentId Id,
    ArmyTypeDefinition Type,
    ArmyCombatStats Stats,
    long Health,
    long Experience);

/// <summary>Health and experience after a battle, in the order the rosters were given.</summary>
internal sealed record BattleResult(
    bool AttackerWon,
    int Rounds,
    IReadOnlyList<(long Health, long Experience)> Attackers,
    IReadOnlyList<(long Health, long Experience)> Defenders);

/// <summary>
/// Resolves one land battle. Hidden behind an interface the way <c>ITradeMarket</c>
/// hides the price rule, because the shipped rule is a placeholder: the executable's
/// loss formula at <c>0x004A8040</c> is not yet traced.
/// </summary>
internal interface IBattleResolver
{
    BattleResult Resolve(
        IReadOnlyList<BattleCombatant> attackers,
        IReadOnlyList<BattleCombatant> defenders,
        int fortLevel,
        IBattleRandom random);
}

/// <summary>
/// The original's <b>strategic</b> auto-resolution loop as far as the binary recovered
/// it, with every unrecovered piece a labelled, chosen placeholder.
/// </summary>
/// <remarks>
/// Binary: the health snapshot, the loop's "both sides hold a regiment above half its
/// snapshot", attacker first, the 1-or-2 weight, the draw modulo 7, artillery's half
/// branch, the scale array [70, 80, 90, 90], and +35 / +20 experience in hundredths to
/// a ceiling of 400. Chosen: the 20-round stop as a defender win, the +1 on the
/// multiplier, the artillery rounding, 25% firepower per medal, indexing the scale by
/// round, the uniform target, loss = shot × 5 / DEF with a floor of 1, entrenchment
/// through DEF, and the fort reduction on artillery. <b>The loss formula is not the
/// original's.</b> See <c>docs/formulas/army.md</c>, rules 12–30.
/// </remarks>
internal sealed class StrategicBattleResolver : IBattleResolver
{
    public static readonly StrategicBattleResolver Instance = new();

    /// <summary>A chosen stop, so engineers against engineers end. A battle stopped here is the defender's.</summary>
    public const int MaxRounds = 20;

    /// <summary>The executable's round-scale array.</summary>
    private static readonly long[] Scale = [70, 80, 90, 90];

    /// <summary>The executable's experience awards, in hundredths.</summary>
    public const long VictoryExperience = 35;

    public const long DefeatExperience = 20;

    /// <summary>The chosen constant that makes an even fight of Regulars break in about four rounds.</summary>
    public const long LossConstant = 5;

    public BattleResult Resolve(
        IReadOnlyList<BattleCombatant> attackers,
        IReadOnlyList<BattleCombatant> defenders,
        int fortLevel,
        IBattleRandom random)
    {
        ArgumentNullException.ThrowIfNull(attackers);
        ArgumentNullException.ThrowIfNull(defenders);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(fortLevel);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fortLevel, WorldState.MaxFortLevel);

        var attackHealth = attackers.Select(static item => item.Health).ToArray();
        var defendHealth = defenders.Select(static item => item.Health).ToArray();
        var attackSnapshot = attackHealth.ToArray();
        var defendSnapshot = defendHealth.ToArray();

        var rounds = 0;
        var stopped = false;
        while (HasEligible(attackHealth, attackSnapshot) && HasEligible(defendHealth, defendSnapshot))
        {
            if (rounds == MaxRounds)
            {
                stopped = true;
                break;
            }

            Volley(attackers, attackHealth, attackSnapshot, defenders, defendHealth, targetsDefend: true, rounds, fortLevel, random);
            Volley(defenders, defendHealth, defendSnapshot, attackers, attackHealth, targetsDefend: false, rounds, fortLevel, random);
            rounds++;
        }

        // The attacker is checked first, as in the executable.
        var attackerWon = !stopped && HasEligible(attackHealth, attackSnapshot);
        return new BattleResult(
            attackerWon,
            rounds,
            Award(attackers, attackHealth, attackerWon),
            Award(defenders, defendHealth, !attackerWon));
    }

    private static bool HasEligible(long[] health, long[] snapshot)
    {
        for (var index = 0; index < health.Length; index++)
        {
            if (health[index] > snapshot[index] / 2)
            {
                return true;
            }
        }

        return false;
    }

    private static void Volley(
        IReadOnlyList<BattleCombatant> firers,
        long[] firerHealth,
        long[] firerSnapshot,
        IReadOnlyList<BattleCombatant> targets,
        long[] targetHealth,
        bool targetsDefend,
        int round,
        int fortLevel,
        IBattleRandom random)
    {
        var live = new List<int>(targets.Count);
        for (var index = 0; index < firers.Count; index++)
        {
            if (firerHealth[index] <= 0)
            {
                continue;
            }

            live.Clear();
            for (var target = 0; target < targets.Count; target++)
            {
                if (targetHealth[target] > 0)
                {
                    live.Add(target);
                }
            }

            if (live.Count == 0)
            {
                return;
            }

            var firer = firers[index];
            var weight = firerHealth[index] > firerSnapshot[index] / 2 ? 2 : 1;
            var multiplier = 1 + (long)(random.Next() % 7);
            if (IsArtillery(firer.Type))
            {
                multiplier = (multiplier + 1) / 2;
            }

            var medals = firer.Experience / Regiment.ExperiencePerMedal;
            var firepower = firer.Stats.FirepowerVsInfantry * (100 + (25 * medals)) / 100m;
            var scale = Scale[Math.Min(round, Scale.Length - 1)];
            var shot = (long)decimal.Floor(weight * multiplier * firepower * scale / 100m);

            var chosen = live[(int)(random.Next() % (ulong)live.Count)];
            var defence = Math.Max(1, targetsDefend ? DefendingDefence(targets[chosen]) : targets[chosen].Stats.Defence);
            var loss = shot > 0 ? Math.Max(1, shot * LossConstant / defence) : 0;
            if (targetsDefend)
            {
                loss = loss * (100 - (10 * fortLevel)) / 100;
            }

            targetHealth[chosen] = Math.Max(0, targetHealth[chosen] - loss);
        }
    }

    /// <summary>
    /// A defender's entrenched DEF where its type has one, except artillery, which
    /// "cannot entrench" (release notes). The manual's 20% is already inside the
    /// entrenched figure, so it is not applied again.
    /// </summary>
    private static long DefendingDefence(BattleCombatant defender) =>
        defender.Stats.EntrenchedDefence is { } entrenched && !IsArtillery(defender.Type)
            ? entrenched
            : defender.Stats.Defence;

    internal static bool IsArtillery(ArmyTypeDefinition type) =>
        type.Category is ArmyCategory.LightArtillery or ArmyCategory.HeavyArtillery;

    private static (long Health, long Experience)[] Award(
        IReadOnlyList<BattleCombatant> side,
        long[] health,
        bool won)
    {
        var gain = won ? VictoryExperience : DefeatExperience;
        var result = new (long Health, long Experience)[side.Count];
        for (var index = 0; index < side.Count; index++)
        {
            var experience = health[index] > 0
                ? Math.Min(Regiment.MaxExperience, side[index].Experience + gain)
                : side[index].Experience;
            result[index] = (health[index], experience);
        }

        return result;
    }
}

/// <summary>
/// Pass 2 of <c>Conflict</c>: every attack, countries in id order and orders in list
/// order, each checked against the live state as it comes to resolve; then healing.
/// </summary>
internal static class LandBattlePlanner
{
    public static IReadOnlyList<TurnEvent> ResolveAttacks(
        WorldState state,
        IReadOnlyList<(CountryId Country, RegimentMoveOrder Order)> attacks,
        HashSet<RegimentId> acted,
        int turnNumber,
        ulong seed,
        IBattleResolver resolver)
    {
        var events = new List<TurnEvent>();
        var ordinal = 0;
        foreach (var (country, order) in attacks)
        {
            if (Check(state, country, order, acted) is { } refusal)
            {
                events.Add(new RegimentMoveRefusedEvent(turnNumber, country, order.Regiments, order.Destination, refusal));
                continue;
            }

            foreach (var regiment in order.Regiments)
            {
                acted.Add(regiment);
            }

            var destination = order.Destination;
            var previousOwner = state.GetProvinceOwner(destination);
            var attackers = order.Regiments
                .Select(id => state.GetRegiment(id)!)
                .OrderBy(static regiment => regiment.Id.Value)
                .ToArray();
            var defenders = previousOwner is { } owner
                ? state.GetRegiments(owner).Where(regiment => regiment.Province == destination).ToArray()
                : [];

            var attackCombatants = Combatants(state, attackers);
            var defendCombatants = Combatants(state, defenders);
            var uncontested = defendCombatants.Length == 0;
            var attackerWon = true;
            if (!uncontested)
            {
                var result = resolver.Resolve(
                    attackCombatants,
                    defendCombatants,
                    state.GetFortLevel(destination),
                    SplitMix64.ForBattle(seed, ordinal++));
                attackerWon = result.AttackerWon;
                var participants = new List<BattleParticipant>();
                var killed = new List<RegimentId>();
                Apply(state, attackCombatants, result.Attackers, BattleSide.Attacker, participants, killed);
                Apply(state, defendCombatants, result.Defenders, BattleSide.Defender, participants, killed);
                events.Add(new BattleFoughtEvent(
                    turnNumber,
                    destination,
                    country,
                    previousOwner!.Value,
                    participants,
                    killed,
                    attackerWon ? country : previousOwner.Value,
                    result.Rounds));
            }

            // A defeated attack leaves its survivors where they attacked from.
            if (attackerWon)
            {
                Capture(state, country, previousOwner, destination, attackers, defenders, uncontested, turnNumber, events);
            }
        }

        return events;
    }

    /// <summary>
    /// +100 health to the ceiling for every regiment named in no accepted move or attack,
    /// after every battle — the binary's amount, cap and "no movement order" reading, at
    /// a chosen time. One event per country, and none when nothing healed.
    /// </summary>
    public static IReadOnlyList<TurnEvent> Heal(WorldState state, HashSet<RegimentId> acted, int turnNumber)
    {
        var events = new List<TurnEvent>();
        foreach (var group in state.GetRegiments()
                     .Where(regiment => !acted.Contains(regiment.Id) && regiment.Health < Regiment.MaxHealth)
                     .GroupBy(static regiment => regiment.Owner)
                     .OrderBy(static group => group.Key.Value))
        {
            var healed = new List<RegimentId>();
            var restored = 0L;
            foreach (var regiment in group)
            {
                var health = Math.Min(Regiment.MaxHealth, regiment.Health + Regiment.HealingPerTurn);
                state.UpdateRegiment(regiment.Id, health, regiment.Experience);
                healed.Add(regiment.Id);
                restored += health - regiment.Health;
            }

            events.Add(new RegimentsHealedEvent(turnNumber, group.Key, healed, restored));
        }

        return events;
    }

    private static RegimentMoveRefusal? Check(
        WorldState state,
        CountryId country,
        RegimentMoveOrder order,
        HashSet<RegimentId> acted)
    {
        if (RegimentMovePlanner.CheckCommon(state, country, order, acted, out var from, out _) is { } refusal)
        {
            return refusal;
        }

        var destination = order.Destination;
        if (!state.Definition.ProvinceAdjacency.AreAdjacent(from, destination))
        {
            return RegimentMoveRefusal.NotAdjacent;
        }

        var owner = state.GetProvinceOwner(destination);
        if (owner == country)
        {
            return RegimentMoveRefusal.AlreadyOwn;
        }

        // An ownerless province may be attacked without war: the manual's anarchy rule
        // applied to the one ownerless state Core has — a chosen reading.
        if (owner is { } target && !state.HasEffectiveHostility(country, target))
        {
            return RegimentMoveRefusal.NotAtWar;
        }

        return null;
    }

    /// <summary>
    /// The regiments that fight: those whose type carries combat numbers. The Generals
    /// have none and stand aside, moving or retreating with their side — a departure the
    /// blueprint does not cover, recorded in <c>docs/formulas/army.md</c>.
    /// </summary>
    private static BattleCombatant[] Combatants(WorldState state, IEnumerable<Regiment> regiments) =>
        regiments
            .Select(regiment => (Regiment: regiment, Type: state.Definition.ArmyTypes[regiment.Type.Value]))
            .Where(static item => item.Type.Stats is not null)
            .Select(static item => new BattleCombatant(
                item.Regiment.Id, item.Type, item.Type.Stats!, item.Regiment.Health, item.Regiment.Experience))
            .ToArray();

    private static void Apply(
        WorldState state,
        BattleCombatant[] side,
        IReadOnlyList<(long Health, long Experience)> after,
        BattleSide label,
        List<BattleParticipant> participants,
        List<RegimentId> killed)
    {
        for (var index = 0; index < side.Length; index++)
        {
            var (health, experience) = after[index];
            participants.Add(new BattleParticipant(
                side[index].Id,
                side[index].Type.Id,
                label,
                side[index].Health,
                health,
                experience - side[index].Experience));
            if (health <= 0)
            {
                _ = state.RemoveRegiment(side[index].Id);
                killed.Add(side[index].Id);
            }
            else
            {
                state.UpdateRegiment(side[index].Id, health, experience);
            }
        }
    }

    private static void Capture(
        WorldState state,
        CountryId attacker,
        CountryId? previousOwner,
        ProvinceId province,
        IEnumerable<Regiment> attackers,
        IEnumerable<Regiment> defenders,
        bool uncontested,
        int turnNumber,
        List<TurnEvent> events)
    {
        var map = state.Definition.Map;
        var capitalLost = previousOwner is { } previous &&
            state.GetCountryCapital(previous) is { } capital &&
            map[capital].Region.Kind == CellRegionKind.Province &&
            map[capital].Region.Province == province;

        var civilians = previousOwner is { } loser
            ? state.GetCivilians(loser)
                .Where(civilian => map[civilian.Cell].Region.Kind == CellRegionKind.Province &&
                                   map[civilian.Cell].Region.Province == province)
                .Select(static civilian => civilian.Id)
                .ToArray()
            : [];

        // At once, so a later attack this turn meets the new owner. SetProvinceOwner
        // invalidates both countries' rail and clears a capital standing here.
        state.SetProvinceOwner(province, attacker);

        foreach (var civilian in civilians)
        {
            _ = state.RemoveCivilian(civilian);
        }

        var movedIn = attackers
            .Where(regiment => state.GetRegiment(regiment.Id) is not null)
            .Select(static regiment => regiment.Id)
            .ToArray();
        foreach (var regiment in movedIn)
        {
            state.MoveRegiment(regiment, province);
        }

        events.Add(new ProvinceCapturedEvent(
            turnNumber, province, previousOwner, attacker, uncontested, movedIn, civilians, capitalLost));

        var survivors = defenders
            .Where(regiment => state.GetRegiment(regiment.Id) is not null)
            .Select(static regiment => regiment.Id)
            .ToArray();
        if (survivors.Length == 0 || previousOwner is not { } owner)
        {
            return;
        }

        // Fall back to the lowest-id neighbour still held, else surrender — a chosen
        // rule from the manual's "flee, and may surrender" (lines 3913–3915).
        var refuge = state.Definition.ProvinceAdjacency.GetNeighbours(province)
            .Where(neighbour => state.GetProvinceOwner(neighbour) == owner)
            .Select(static neighbour => (ProvinceId?)neighbour)
            .FirstOrDefault();
        if (refuge is { } to)
        {
            foreach (var regiment in survivors)
            {
                state.MoveRegiment(regiment, to);
            }

            events.Add(new RegimentsRetreatedEvent(turnNumber, owner, survivors, province, to));
        }
        else
        {
            foreach (var regiment in survivors)
            {
                _ = state.RemoveRegiment(regiment);
            }

            events.Add(new RegimentsSurrenderedEvent(turnNumber, owner, survivors, province));
        }
    }
}
