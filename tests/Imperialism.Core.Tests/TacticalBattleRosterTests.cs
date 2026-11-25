using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class TacticalBattleRosterTests
{
    [Fact]
    public void RosterPreservesSidesAndTheirRegimentOrder()
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
    public void RosterRejectsAnEmptySideSharedCountryOrWrongProvince()
    {
        var regiment = new InitialArmy(new ProvinceId(0), new ArmyTypeId(0), 1);

        Assert.Throws<ArgumentException>(() => new TacticalBattleRoster(
            new ProvinceId(0), new CountryId(0), new CountryId(0), [regiment], [regiment]));
        Assert.Throws<ArgumentException>(() => new TacticalBattleRoster(
            new ProvinceId(0), new CountryId(0), new CountryId(1), [], [regiment]));
        var inexperiencedRegiment = new InitialArmy(new ProvinceId(0), new ArmyTypeId(0), 0);
        var roster = new TacticalBattleRoster(
            new ProvinceId(0), new CountryId(0), new CountryId(1),
            [inexperiencedRegiment], [regiment]);
        Assert.Single(roster.Attackers);
        Assert.Throws<ArgumentException>(() => new TacticalBattleRoster(
            new ProvinceId(0), new CountryId(0), new CountryId(1),
            [new InitialArmy(new ProvinceId(1), new ArmyTypeId(0), 1)], [regiment]));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(30)]
    public void ArmyTypeIsLimitedToTheRecoveredExecutableTable(int type)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArmyTypeId(type));
    }
}
