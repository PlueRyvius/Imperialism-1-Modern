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

    [Fact]
    public void ArmyTypeRejectsANegativeId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArmyTypeId(-1));
    }

    /// <summary>
    /// The original executable's 30-row army table is a limit of the 1997 files
    /// and stops at the importer (issue #47). A modern world may name army types
    /// past it, and Core builds and fights with them.
    /// </summary>
    [Fact]
    public void WorldAcceptsAndFightsWithArmyTypesBeyondTheOriginalTable()
    {
        var map = new MapDefinition(
            new MapDimensions(1, 1),
            [new CellDefinition(
                new CellIndex(0),
                new HexCoord(0, 0),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(0)),
                [])],
            [new ProvinceDefinition(new ProvinceId(0), "Province")]);
        var scenario = new ScenarioDefinition(
            "Modern armies",
            1815,
            [new CountryId(0)],
            initialArmies:
            [
                new InitialArmy(new ProvinceId(0), new ArmyTypeId(30), 2),
                new InitialArmy(new ProvinceId(0), new ArmyTypeId(1_000), 1),
            ]);
        var world = new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(0), "A"), new CountryDefinition(new CountryId(1), "B")],
            scenario);

        var armies = world.Scenario.InitialArmies;
        Assert.Equal([30, 1_000], armies.Select(static army => army.Type.Value));

        var roster = new TacticalBattleRoster(
            new ProvinceId(0), new CountryId(1), new CountryId(0), [armies[1]], [armies[0]]);
        Assert.Equal(new ArmyTypeId(1_000), roster.Attackers[0].Type);
        Assert.Equal(new ArmyTypeId(30), roster.Defenders[0].Type);
    }
}
