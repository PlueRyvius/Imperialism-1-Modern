using Imperialism.Content;
using Imperialism.Core;
using Xunit;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// Core sets an eliminated country's orders aside before anything is planned and reports
/// one <see cref="EliminatedCountryOrdersIgnoredEvent"/>. Every preview of an order that
/// country drafts must say the same, and must claim nothing for it: the Armoury, the
/// Shipyard, overtures and the moves, each checked against a resolved turn.
/// </summary>
public sealed class EliminatedCountryPreviewTests
{
    private const string Scenario = "scenario.demo";
    private static readonly CountryId Blue = new(0);
    private static readonly CountryId Red = new(1);
    private static readonly CountryId Green = new(2);
    private static readonly ProvinceId Home = new(0);
    private static readonly ProvinceId West = new(2);
    private static readonly ProvinceId East = new(3);
    private static readonly ShipTypeId Frigate = new(1);
    private static readonly CivilianUnitId Farmer = new(1);
    private static readonly CellIndex EastCell = new(3);

    [Fact]
    public void EveryOrderOfAnEliminatedCountryPreviewsAsSetAsideAndClaimsNothing()
    {
        var (package, state, survivor) = EliminateBlue();
        var draft = new OrdersDraft(package.GetWorld(Scenario), Blue);
        draft.SetRegimentPurchase(ArmyCategory.RegularInfantry, 1);
        draft.SetShipBuild(Frigate, 1);
        draft.SetOverture(DiplomaticOvertureKind.Consulate, Green, true);
        draft.SetRegimentMove([survivor], West);
        draft.SetWork(Farmer, EastCell);

        var preview = OrdersPreview.Create(package, Scenario, state, draft);
        Assert.True(preview.OrdersSetAside);
        Assert.True(OrdersPreview.CreateUnitOrders(package, Scenario, state, draft, Farmer).OrdersSetAside);

        var armoury = preview.Armoury.Categories.Single(row => row.Category == ArmyCategory.RegularInfantry);
        Assert.Equal(OrderWarning<RegimentPurchaseRefusal>.SetAside, armoury.Warning);
        Assert.Equal(0, preview.Armoury.CashClaimed);
        Assert.Equal(0, preview.Armoury.LabourWithheld);

        Assert.Equal(OrderWarning<ShipBuildRefusal>.SetAside, preview.Shipyard.Hulls[Frigate.Value].Warning);

        var move = Assert.Single(preview.ArmyMoves.Moves);
        Assert.Equal(ArmyMoveVerb.March, move.Verb);
        Assert.Equal(OrderWarning<RegimentMoveRefusal>.SetAside, move.Warning);
        Assert.Equal(0, preview.ArmyMoves.RailCommitted);

        var diplomacy = preview.Diplomacy;
        Assert.True(diplomacy.OrdersSetAside);
        Assert.Equal(0, diplomacy.ClaimedByArmoury);
        Assert.Equal(0, diplomacy.ClaimedByOvertures);
        Assert.Equal(0, Assert.Single(diplomacy.Overtures).CashCost);
        Assert.Equal(diplomacy.CashAtOpen, diplomacy.CashAfterOvertures);

        Assert.True(preview.Civilians.OrdersSetAside);
        Assert.Equal(0, preview.Civilians.CommittedCost);
        Assert.Equal(0, preview.Trade.ClaimedByArmoury);
        Assert.Equal(0, preview.Technology.ClaimedByArmoury);

        // Every destination says the same thing about the selected regiment.
        var targets = OrdersPreview.CreateArmyMoves(package, Scenario, state, draft, [survivor]);
        Assert.All(targets.Destinations, item => Assert.Equal(OrderWarning<RegimentMoveRefusal>.SetAside, item.Warning));

        var cash = state.GetCash(Blue);
        var arms = state.GetAvailableQuantity(Blue, Arms(state));
        var regiments = state.GetRegiments(Blue).Count;
        var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 3).Events;

        Assert.Contains(events, item => item is EliminatedCountryOrdersIgnoredEvent { Country.Value: 0 });
        Assert.DoesNotContain(events, item => item is RegimentsPurchasedEvent { Country.Value: 0 }
            or RegimentPurchaseRefusedEvent { Country.Value: 0 }
            or ShipsBuiltEvent { Country.Value: 0 }
            or ShipBuildRefusedEvent { Country.Value: 0 }
            or RegimentsMovedEvent { Country.Value: 0 }
            or RegimentMoveRefusedEvent { Country.Value: 0 }
            or DiplomaticOvertureRefusedEvent { Country.Value: 0 }
            or CivilianWorkBegunEvent { Country.Value: 0 }
            or CivilianOrderRefusedEvent { Country.Value: 0 });
        Assert.Equal(cash, state.GetCash(Blue));
        Assert.Equal(diplomacy.CashAfterOvertures, state.GetCash(Blue));
        Assert.Equal(arms, state.GetAvailableQuantity(Blue, Arms(state)));
        Assert.Equal(regiments, state.GetRegiments(Blue).Count);
        Assert.Equal(East, state.GetRegiment(survivor)!.Province);
        Assert.False(state.HoldsDiplomaticTie(Blue, Green, DiplomaticTie.Consulate));
        Assert.DoesNotContain(state.Fleets, fleet => fleet.Country == Blue && fleet.Type == Frigate);
    }

    [Fact]
    public void ALiveCountrysSameDraftIsNotSetAside()
    {
        // The control: the same orders from a country that still stands are judged as usual.
        var package = Compile();
        var state = new WorldState(package.GetWorld(Scenario));
        var survivor = state.CreateRegiment(Blue, East, new ArmyTypeId(0));
        var draft = new OrdersDraft(package.GetWorld(Scenario), Blue);
        draft.SetRegimentPurchase(ArmyCategory.RegularInfantry, 1);
        draft.SetShipBuild(Frigate, 1);
        draft.SetOverture(DiplomaticOvertureKind.Consulate, Green, true);
        draft.SetRegimentMove([survivor], West);
        draft.SetWork(Farmer, EastCell);

        var preview = OrdersPreview.Create(package, Scenario, state, draft);
        Assert.False(preview.OrdersSetAside);
        Assert.False(OrdersPreview.CreateUnitOrders(package, Scenario, state, draft, Farmer).OrdersSetAside);
        Assert.Equal(100, preview.Civilians.CommittedCost);
        Assert.False(preview.Diplomacy.OrdersSetAside);
        Assert.False(preview.Civilians.OrdersSetAside);
        Assert.Null(preview.Armoury.Categories.Single(row => row.Category == ArmyCategory.RegularInfantry).Warning);
        Assert.Equal(500, preview.Armoury.CashClaimed);
        Assert.Null(preview.Shipyard.Hulls[Frigate.Value].Warning);
        Assert.Null(Assert.Single(preview.ArmyMoves.Moves).Warning);
        Assert.Equal(500, preview.Diplomacy.ClaimedByOvertures);

        var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events;
        Assert.DoesNotContain(events, item => item is EliminatedCountryOrdersIgnoredEvent);
        Assert.Contains(events, item => item is RegimentsPurchasedEvent { Country.Value: 0 });
        Assert.Contains(events, item => item is ShipsBuiltEvent { Country.Value: 0 });
        Assert.Contains(events, item => item is CivilianWorkBegunEvent { Country.Value: 0, Paid: 100 });
        Assert.Equal(West, state.GetRegiment(survivor)!.Province);
        Assert.True(state.HoldsDiplomaticTie(Blue, Green, DiplomaticTie.Consulate));
    }

    /// <summary>Red declares war, then takes Blue's undefended capital: Blue is eliminated.</summary>
    private static (CompiledWorldPackage Package, WorldState State, RegimentId Survivor) EliminateBlue()
    {
        var package = Compile();
        var state = new WorldState(package.GetWorld(Scenario));
        var survivor = state.CreateRegiment(Blue, East, new ArmyTypeId(0));
        var invader = state.CreateRegiment(Red, new ProvinceId(1), new ArmyTypeId(0));
        TurnResolver.Resolve(state, new TurnOrders([
            new CountryTurnOrders(Blue), new CountryTurnOrders(Red, declareWar: [Blue]), new CountryTurnOrders(Green)]), 1);
        var capture = TurnResolver.Resolve(state, new TurnOrders([
            new CountryTurnOrders(Blue),
            new CountryTurnOrders(Red, regimentMoves: [new RegimentMoveOrder([invader], Home)]),
            new CountryTurnOrders(Green)]), 2);
        Assert.Contains(capture.Events, item => item is CountryEliminatedEvent { Country.Value: 0 });
        return (package, state, survivor);
    }

    private static CommodityId Arms(WorldState state) =>
        state.Definition.Commodities.Single(static item => item.Name == "Arms").Id;

    /// <summary>
    /// The newspaper fixture's four cells — Home, Far, West, East in a row — with Blue
    /// holding West and East, a Minor Nation to open a consulate in, a Regular for one
    /// arms and $500, a Frigate for one lumber, and a Farmer on East's grain at $100 a rung.
    /// </summary>
    private static CompiledWorldPackage Compile()
    {
        var document = TurnReportFixture.CreateDocument();
        document.Diplomacy = new DiplomacyContentSettings
        {
            ConsulateCost = 500, EmbassyCost = 5000, SubsidyPercents = [5, 10],
        };
        document.Countries =
        [
            .. document.Countries,
            new CountryContentDefinition { Key = "country.green", Name = "Green Duchy", IsGreatPower = false },
        ];
        document.Commodities =
        [
            .. document.Commodities,
            new CommodityContentDefinition
            {
                Key = "commodity.arms", Name = "Arms", Category = CommodityCategory.Material, WorldPrice = 300, TradeOrder = 2,
            },
        ];
        document.ShipTypes =
        [
            document.ShipTypes[0],
            new ShipTypeContentDefinition
            {
                Key = "ship.frigate",
                Name = "Frigate",
                SeaZones = 2,
                BuildCost = [new CommodityQuantityContent { Commodity = "commodity.lumber", Quantity = 1 }],
                Combat = new ShipCombatContent { Firepower = 100, Range = 1, Armour = 10, HullScale = 600, BattleSpeed = 2 },
            },
        ];
        document.CivilianTypes =
        [
            new CivilianTypeContentDefinition
            {
                Key = "civilian.farmer", Name = "Farmer", WorkTurns = 3, Work = CivilianWorkKind.Improve,
            },
        ];
        document.Resources[0].ImprovedBy = "civilian.farmer";
        document.Improvement = new ImprovementContentSettings { CashCostByDevelopmentLevel = [0, 100, 1000, 3000] };
        document.ArmyTypes =
        [
            new ArmyTypeContentDefinition
            {
                Key = "army.regulars",
                Name = "Regulars",
                Category = ArmyCategory.RegularInfantry,
                Era = 1,
                Purchase = new ArmyPurchaseContent
                {
                    WorkerGrade = WorkerGrade.Untrained,
                    Bill = [new CommodityQuantityContent { Commodity = "commodity.arms", Quantity = 1 }],
                    Cash = 500,
                },
                Arms = 1,
            },
        ];
        document.Map.Provinces =
        [
            .. document.Map.Provinces,
            new NamedContentDefinition { Key = "province.west", Name = "West" },
            new NamedContentDefinition { Key = "province.east", Name = "East" },
        ];
        document.Map.Cells[2] = new CellContentDocument
        {
            Terrain = "terrain.plains", Region = new CellRegionContent { Province = "province.west" },
        };
        document.Map.Cells[3] = new CellContentDocument
        {
            Terrain = "terrain.plains", Region = new CellRegionContent { Province = "province.east" },
            Resources = ["resource.grain"],
        };
        foreach (var scenario in document.Scenarios)
        {
            scenario.ProvinceOwners =
            [
                .. scenario.ProvinceOwners,
                new ProvinceOwnerContent { Province = "province.west", Country = "country.blue" },
                new ProvinceOwnerContent { Province = "province.east", Country = "country.blue" },
            ];
            scenario.Civilians = [new CivilianContent { Country = "country.blue", Type = "civilian.farmer", Cell = 3 }];
            scenario.InitialInventory =
            [
                .. scenario.InitialInventory,
                new InitialInventoryContent { Country = "country.blue", Commodity = "commodity.arms", Quantity = 2 },
                new InitialInventoryContent { Country = "country.blue", Commodity = "commodity.lumber", Quantity = 4 },
            ];
        }

        return WorldContentCompiler.CompilePackage(document);
    }
}
