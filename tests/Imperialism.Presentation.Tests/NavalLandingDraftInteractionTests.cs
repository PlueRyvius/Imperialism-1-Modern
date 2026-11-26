using Imperialism.Content;
using Imperialism.Core;
using Xunit;
namespace Imperialism.Presentation.Tests;
public sealed class NavalLandingDraftInteractionTests
{
    private const string Scenario = "scenario.demo";
    private static readonly CountryId Blue = new(0), Red = new(1);
    private static readonly ProvinceId Home = new(0), Far = new(1);
    private static readonly SeaZoneId SeaB = new(1), SeaC = new(2);

    [Theory]
    [InlineData("keep", false)]
    [InlineData("none", false)]
    [InlineData("restamp", false)]
    [InlineData("move", false)]
    [InlineData("patrol", false)]
    [InlineData("blockade", false)]
    [InlineData("keep", true)]
    [InlineData("none", true)]
    [InlineData("restamp", true)]
    [InlineData("move", true)]
    [InlineData("patrol", true)]
    [InlineData("blockade", true)]
    public void ArmyPreviewAccountsForDraftedChangesToItsLandingSite(string command, bool destinationList)
    {
        var package = Compile();
        var state = new WorldState(package.GetWorld(Scenario));
        var regiment = state.CreateRegiment(Blue, Home, new ArmyTypeId(0));
        var fleet = state.Fleets.Single(x => x.Country == Blue && x.SeaZone == SeaC).Id;
        // Establish the site and declare war on turn one; it is ready on turn two.
        var setup = TurnResolver.Resolve(state, new TurnOrders([
            new CountryTurnOrders(Blue, declareWar: [Red], naval: [NavalOrder.Assemble([fleet], NavalMission.Landing(Far))]),
            new CountryTurnOrders(Red)]), 1);
        Assert.Contains(setup.Events, x => x is TaskForceLandingOrderedEvent { Country.Value: 0 });
        var force = Assert.Single(state.TaskForces).Id;
        var draft = new OrdersDraft(state.Definition, Blue);
        switch (command)
        {
            case "none": draft.SetNavalCommand(force, NavalMission.None); break;
            case "restamp": draft.SetNavalCommand(force, NavalMission.Landing(Far)); break;
            case "move": draft.SetNavalCommand(force, NavalMission.MoveTo(SeaB)); break;
            case "patrol": draft.SetNavalCommand(force, NavalMission.Patrol); break;
            case "blockade": draft.SetNavalCommand(force, NavalMission.Blockade(Red)); break;
        }
        draft.SetRegimentMove([regiment], Far);
        var preview = OrdersPreview.Create(package, Scenario, state, draft);
        var targets = OrdersPreview.CreateArmyMoves(package, Scenario, state, draft, [regiment]);
        var warning = destinationList ? targets.Destinations.Single(x => x.Province == Far).Warning
            : Assert.Single(preview.ArmyMoves.Moves).Warning;
        Assert.All(preview.Fleet.Orders, x => Assert.Null(x.Warning));
        var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 2).Events;
        var expected = command is "keep" or "none" ? (RegimentMoveRefusal?)null : RegimentMoveRefusal.NotAdjacent;
        var actual = events.OfType<RegimentMoveRefusedEvent>().SingleOrDefault(x => x.Country == Blue);
        Assert.Equal(expected, actual?.Reason);
        Assert.Equal(expected is null ? Far : Home, state.GetRegiment(regiment)!.Province);
        Assert.Equal(expected, warning?.Refusal);
    }
    [Fact]
    public void AssembliesAndCommandsMapToCoreAndMatchNinetyFixtureResolutions()
    {
        var missions = new NavalMission[] { NavalMission.None, NavalMission.Patrol,
            NavalMission.MoveTo(new SeaZoneId(0)), NavalMission.MoveTo(new SeaZoneId(1)),
            NavalMission.MoveTo(new SeaZoneId(2)), NavalMission.MoveTo(new SeaZoneId(3)),
            NavalMission.Blockade(Blue), NavalMission.Blockade(Red), NavalMission.Landing(Home), NavalMission.Landing(Far) };
        var cases = 0;
        foreach (var selection in Enumerable.Range(0, 9))
        foreach (var mission in missions)
        {
            var package = Compile();
            var state = new WorldState(package.GetWorld(Scenario));
            var draft = new OrdersDraft(state.Definition, Blue);
            FleetId Find(CountryId country, string type, SeaZoneId? zone) => state.Fleets.Single(x =>
                x.Country == country && state.Definition.ShipTypes[x.Type.Value].Name == type && x.SeaZone == zone).Id;
            var a = Find(Blue, "Frigate", new SeaZoneId(0));
            var g = Find(Blue, "Gunboat", new SeaZoneId(0));
            var c = Find(Blue, "Frigate", new SeaZoneId(2));
            FleetId[] named = selection switch
            {
                0 => [a], 1 => [g], 2 => [c], 3 => [Find(Blue, "Frigate", new SeaZoneId(3))],
                4 => [Find(Blue, "Trader", null)], 5 => [Find(Red, "Frigate", new SeaZoneId(1))],
                6 => [new FleetId(99)], 7 => [a, g], _ => [a, c],
            };
            draft.SetNavalAssembly(named, mission);
            var built = Assert.IsType<NavalOrder.AssembleOrder>(Assert.Single(draft.Build().Naval));
            Assert.Equal(named, built.Fleets);
            Assert.Same(mission, built.Mission);
            var row = Assert.Single(OrdersPreview.Create(package, Scenario, state, draft).Fleet.Orders);
            var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events;
            var refused = events.OfType<NavalOrderRefusedEvent>().SingleOrDefault(x => x.Country == Blue);
            Assert.Equal(refused?.Reason, row.Warning?.Refusal);
            Assert.False(row.Warning?.OrdersSetAside ?? false);
            Assert.Equal(refused is null ? 1 : 0, state.TaskForces.Count);
            if (refused is null)
            {
                var force = Assert.Single(state.TaskForces);
                // Any first click is replaced by the next command, so only the latter is sent.
                var next = NavalMission.None;
                draft.ClearNavalOrders();
                draft.SetNavalCommand(force.Id, NavalMission.Patrol);
                draft.SetNavalCommand(force.Id, next);
                var command = Assert.IsType<NavalOrder.CommandOrder>(Assert.Single(draft.Build().Naval));
                Assert.Equal(force.Id, command.TaskForce);
                Assert.Same(next, command.Mission);
            }
            draft.AdvanceTurn();
            Assert.Empty(draft.Build().Naval);
            cases++;
        }
        Assert.Equal(90, cases);
    }

    private static CompiledWorldPackage Compile()
    {
        var document = TurnReportFixture.CreateDocument();
        document.Commodities = [.. document.Commodities, new CommodityContentDefinition
        { Key = "commodity.arms", Name = "Arms", Category = CommodityCategory.Material }];
        document.ArmyTypes = [new ArmyTypeContentDefinition
        {
            Key = "army.regulars", Name = "Regulars", Category = ArmyCategory.RegularInfantry, Era = 1, Arms = 1,
            Purchase = new ArmyPurchaseContent
            {
                WorkerGrade = WorkerGrade.Untrained, Cash = 1,
                Bill = [new CommodityQuantityContent { Commodity = "commodity.arms", Quantity = 1 }],
            },
        }];
        var combat = new ShipCombatContent { Firepower = 100, Range = 1, Armour = 10, HullScale = 600, BattleSpeed = 2 };
        document.ShipTypes =
        [
            new ShipTypeContentDefinition { Key = "ship.trader", Name = "Trader", Cargo = 2, SeaZones = 1 },
            new ShipTypeContentDefinition { Key = "ship.frigate", Name = "Frigate", SeaZones = 2, Combat = combat },
            new ShipTypeContentDefinition { Key = "ship.gunboat", Name = "Gunboat", SeaZones = 1, Combat = combat },
        ];

        document.ShipTypes[1].BuildCost = [new CommodityQuantityContent { Commodity = "commodity.arms", Quantity = 1 }];
        document.Map.Width = 7;
        document.Map.SeaZones =
        [
            new NamedContentDefinition { Key = "sea.a", Name = "Sea A" },
            new NamedContentDefinition { Key = "sea.b", Name = "Sea B" },
            new NamedContentDefinition { Key = "sea.c", Name = "Sea C" },
            new NamedContentDefinition { Key = "sea.d", Name = "Sea D" },
        ];
        CellContentDocument Land(string province, string? resource = null, bool site = false) => new()
        {
            Terrain = "terrain.plains",
            Region = new CellRegionContent { Province = province },
            Resources = resource is null ? [] : [resource],
            HasSettlementSite = site,
        };
        CellContentDocument Sea(string zone) => new()
        {
            Terrain = "terrain.ocean",
            Region = new CellRegionContent { SeaZone = zone },
        };
        document.Map.Cells =
        [
            Land("province.home", "resource.grain", site: true),
            Sea("sea.a"),
            Sea("sea.b"),
            Sea("sea.c"),
            Land("province.far", "resource.timber", site: true),
            Sea("sea.d"),
            Land("province.far"),
        ];

        foreach (var scenario in document.Scenarios)
        {
            scenario.Capitals =
            [
                new CountryCapitalContent { Country = "country.blue", Cell = 0 },
                new CountryCapitalContent { Country = "country.red", Cell = 4 },
            ];
            scenario.Ships =
            [
                new ShipContent { Country = "country.blue", Type = "ship.frigate", SeaZoneKey = "sea.a", Count = 2 },
                new ShipContent { Country = "country.blue", Type = "ship.gunboat", SeaZoneKey = "sea.a", Count = 1 },
                new ShipContent { Country = "country.blue", Type = "ship.frigate", SeaZoneKey = "sea.c", Count = 1 },
                new ShipContent { Country = "country.blue", Type = "ship.frigate", SeaZoneKey = "sea.d", Count = 1 },
                new ShipContent { Country = "country.blue", Type = "ship.trader", Count = 3 },
                new ShipContent { Country = "country.red", Type = "ship.frigate", SeaZoneKey = "sea.b", Count = 1 },
            ];
        }

        return WorldContentCompiler.CompilePackage(document);
    }

}
