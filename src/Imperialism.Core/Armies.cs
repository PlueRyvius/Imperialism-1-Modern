namespace Imperialism.Core;

/// <summary>
/// One army stack placed by a scenario. The original <c>army</c> record is
/// <c>[province, type, count]</c>; ownership is deliberately read from the
/// province rather than duplicated here.
/// </summary>
public readonly record struct InitialArmy(
    ProvinceId Province,
    ArmyTypeId Type,
    long Count);

/// <summary>
/// A validated, headless tactical-battle input. It deliberately contains no
/// deployment, orders, losses, or resolution rule: the executable evidence
/// currently proves the roster boundary, not those mechanics.
/// </summary>
public sealed class TacticalBattleRoster
{
    private readonly IReadOnlyList<InitialArmy> _attackers;
    private readonly IReadOnlyList<InitialArmy> _defenders;

    public TacticalBattleRoster(
        ProvinceId province,
        CountryId attacker,
        CountryId defender,
        IEnumerable<InitialArmy> attackers,
        IEnumerable<InitialArmy> defenders)
    {
        ArgumentNullException.ThrowIfNull(attackers);
        ArgumentNullException.ThrowIfNull(defenders);
        if (attacker == defender)
        {
            throw new ArgumentException("A tactical battle needs two distinct countries.");
        }

        var attackerArray = attackers.ToArray();
        var defenderArray = defenders.ToArray();
        ValidateSide(province, attackerArray, nameof(attackers));
        ValidateSide(province, defenderArray, nameof(defenders));

        Province = province;
        Attacker = attacker;
        Defender = defender;
        _attackers = Array.AsReadOnly(attackerArray);
        _defenders = Array.AsReadOnly(defenderArray);
    }

    public ProvinceId Province { get; }

    public CountryId Attacker { get; }

    public CountryId Defender { get; }

    public IReadOnlyList<InitialArmy> Attackers => _attackers;

    public IReadOnlyList<InitialArmy> Defenders => _defenders;

    private static void ValidateSide(
        ProvinceId province,
        IEnumerable<InitialArmy> side,
        string parameterName)
    {
        if (!side.Any())
        {
            throw new ArgumentException("A tactical battle side cannot be empty.", parameterName);
        }

        foreach (var army in side)
        {
            if (army.Province != province)
            {
                throw new ArgumentException(
                    "Every tactical battle stack must be in the battle province.",
                    parameterName);
            }

            if (army.Count <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "A tactical battle stack must contain at least one unit.");
            }
        }
    }
}
