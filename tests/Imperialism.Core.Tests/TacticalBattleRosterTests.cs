using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class TacticalBattleRosterTests
{
    [Fact]
    public void RosterPreservesSidesAndTheirStackOrder()
    {
        var roster = new TacticalBattleRoster(
            new ProvinceId(4),
            new CountryId(0),
            new CountryId(1),
            [
                new InitialArmy(new ProvinceId(4), new ArmyTypeId(0), 4),
                new InitialArmy(new ProvinceId(4), new ArmyTypeId(7), 1),
            ],
            [new InitialArmy(new ProvinceId(4), new ArmyTypeId(8), 6)]);

        Assert.Equal(new ProvinceId(4), roster.Province);
        Assert.Equal(new CountryId(0), roster.Attacker);
        Assert.Equal(new CountryId(1), roster.Defender);
        Assert.Equal(2, roster.Attackers.Count);
        Assert.Equal(new ArmyTypeId(7), roster.Attackers[1].Type);
        Assert.Single(roster.Defenders);
    }

    [Fact]
    public void RosterRejectsAnEmptySideSharedCountryOrEmptyStack()
    {
        var stack = new InitialArmy(new ProvinceId(0), new ArmyTypeId(0), 1);

        Assert.Throws<ArgumentException>(() => new TacticalBattleRoster(
            new ProvinceId(0), new CountryId(0), new CountryId(0), [stack], [stack]));
        Assert.Throws<ArgumentException>(() => new TacticalBattleRoster(
            new ProvinceId(0), new CountryId(0), new CountryId(1), [], [stack]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TacticalBattleRoster(
            new ProvinceId(0), new CountryId(0), new CountryId(1),
            [new InitialArmy(new ProvinceId(0), new ArmyTypeId(0), 0)], [stack]));
        Assert.Throws<ArgumentException>(() => new TacticalBattleRoster(
            new ProvinceId(0), new CountryId(0), new CountryId(1),
            [new InitialArmy(new ProvinceId(1), new ArmyTypeId(0), 1)], [stack]));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(30)]
    public void ArmyTypeIsLimitedToTheRecoveredExecutableTable(int type)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArmyTypeId(type));
    }
}
