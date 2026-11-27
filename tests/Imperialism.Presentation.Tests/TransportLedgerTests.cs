using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;
using static Imperialism.Presentation.Tests.OrdersFixture;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// The rebuilt Transport ledger's rows and their arrows: layout note decisions D6–D10
/// in <c>docs/look/transport-layout.md</c>. Expected values come from those decisions,
/// with the fixture's yields chosen so each rule decides something.
/// </summary>
public sealed class TransportLedgerTests
{
    private const string FishLivestock = "commodity.fish+commodity.livestock";
    private const string CottonWool = "commodity.cotton+commodity.wool";

    [Fact]
    public void FishAndLivestockAndCottonAndWoolAreEachOneRowWithSummedFigures()
    {
        var (package, state) = World();
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetTransport(Commodity(package, "commodity.fish"), 2);
        draft.SetTransport(Commodity(package, "commodity.livestock"), 1);

        var transport = Preview(package, state, draft);
        var pair = transport.GetRow(FishLivestock)!;

        Assert.Equal("Fish & Livestock", pair.Name);
        Assert.Equal(["commodity.fish", "commodity.livestock"], pair.Members.Select(static member => member.CommodityKey));
        Assert.Equal(
            [Commodity(package, "commodity.fish"), Commodity(package, "commodity.livestock")],
            pair.Members.Select(static member => member.Commodity));
        Assert.Equal((3L, 3L, 8L), (pair.Requested, pair.Committed, pair.Available));
        Assert.Equal("Cotton & Wool", transport.GetRow(CottonWool)!.Name);
        Assert.Equal(5L, transport.GetRow(CottonWool)!.Available);
        Assert.Null(transport.GetRow("commodity.fish"));
        Assert.Null(transport.GetRow("commodity.livestock"));
    }

    [Fact]
    public void APairIsSplitWhenOneOfItsCommoditiesIsAbsent()
    {
        var (package, state) = World(static document =>
        {
            document.Commodities = document.Commodities.Where(static item => item.Key != "commodity.livestock").ToArray();
            document.Resources = document.Resources.Where(static item => item.Key != "resource.livestock").ToArray();
            document.Map.Cells[0].Resources = document.Map.Cells[0].Resources.Where(static key => key != "resource.livestock").ToArray();
        });

        var transport = Preview(package, state, new OrdersDraft(state.Definition, Blue));

        var fish = transport.GetRow("commodity.fish")!;
        Assert.Equal("Fish", fish.Name);
        Assert.Equal((TransportLedgerColumn.Left, 0, (int?)4001), (fish.Column, fish.Slot, fish.Strip));
        Assert.Null(transport.GetRow(FishLivestock));
        Assert.NotNull(transport.GetRow(CottonWool));
    }

    [Fact]
    public void RaiseGoesToTheMostRoomAndTiesToTheLowerCommodityId()
    {
        var (package, state) = World();
        var fish = Commodity(package, "commodity.fish");
        var livestock = Commodity(package, "commodity.livestock");
        Assert.True(fish.Value < livestock.Value);
        var draft = new OrdersDraft(state.Definition, Blue);
        var transport = Preview(package, state, draft);

        // Both have 4 available and nothing ordered: a tie, so fish.
        Assert.True(draft.RaiseTransportRow(transport, FishLivestock));
        Assert.Equal([(fish, 1L)], draft.Transport);

        // Fish has room 3, livestock 4: livestock.
        Assert.True(draft.RaiseTransportRow(transport, FishLivestock));
        Assert.Equal([(fish, 1L), (livestock, 1L)], draft.Transport);

        // Livestock ordered 3 of 4, fish 0 of 4: fish has the most room.
        draft.SetTransport(fish, 0);
        draft.SetTransport(livestock, 3);
        Assert.True(draft.RaiseTransportRow(transport, FishLivestock));
        Assert.Equal([(fish, 1L), (livestock, 3L)], draft.Transport);
    }

    [Fact]
    public void LowerTakesFromTheMostOrderedAndTiesToTheHigherCommodityId()
    {
        var (package, state) = World();
        var fish = Commodity(package, "commodity.fish");
        var livestock = Commodity(package, "commodity.livestock");
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetTransport(fish, 2);
        draft.SetTransport(livestock, 1);
        var transport = Preview(package, state, draft);

        Assert.True(draft.LowerTransportRow(transport, FishLivestock));
        Assert.Equal([(fish, 1L), (livestock, 1L)], draft.Transport);

        // A tie: livestock, the higher id.
        Assert.True(draft.LowerTransportRow(transport, FishLivestock));
        Assert.Equal([(fish, 1L), (livestock, 0L)], draft.Transport);

        Assert.True(draft.LowerTransportRow(transport, FishLivestock));
        Assert.False(draft.LowerTransportRow(transport, FishLivestock));
        Assert.Equal([(fish, 0L), (livestock, 0L)], draft.Transport);
        Assert.False(Preview(package, state, draft).GetRow(FishLivestock)!.CanLower);
    }

    [Fact]
    public void RaiseIsRefusedAtTheSharedCapacity()
    {
        var (package, state) = World(capacity: 5);
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetTransport(Commodity(package, "commodity.grain"), 4);
        var transport = Preview(package, state, draft);
        Assert.True(transport.GetRow(FishLivestock)!.CanRaise);

        Assert.True(draft.RaiseTransportRow(transport, FishLivestock));
        var full = draft.Transport.ToArray();

        Assert.False(draft.RaiseTransportRow(transport, FishLivestock));
        Assert.False(draft.RaiseTransportRow(transport, CottonWool));
        Assert.Equal(full, draft.Transport);
        Assert.All(Preview(package, state, draft).Rows, static row => Assert.False(row.CanRaise));
    }

    [Fact]
    public void AnUnlimitedNetworkHasNoCapacityCap()
    {
        var (package, state) = World(document => document.Transport = null);
        var draft = new OrdersDraft(state.Definition, Blue);
        var transport = Preview(package, state, draft);
        Assert.True(transport.IsUnlimited);

        Assert.True(draft.SetTransportRow(transport, FishLivestock, 8));
        Assert.True(draft.SetTransportRow(transport, CottonWool, 5));
        Assert.Equal(13, draft.Transport.Sum(static row => row.Quantity));
    }

    [Fact]
    public void RaiseIsRefusedWhenEveryMemberHasReachedItsAvailable()
    {
        var (package, state) = World();
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetTransport(Commodity(package, "commodity.cotton"), 3);
        draft.SetTransport(Commodity(package, "commodity.wool"), 2);
        var transport = Preview(package, state, draft);
        var row = transport.GetRow(CottonWool)!;
        Assert.Equal((5L, 5L), (row.Requested, row.Available));
        Assert.False(row.CanRaise);

        Assert.False(draft.RaiseTransportRow(transport, CottonWool));
        Assert.Equal(5, draft.Transport.Sum(static entry => entry.Quantity));
    }

    [Fact]
    public void SetTransportRowClampsThenStepsOneUnitAtATime()
    {
        var (package, state) = World();
        var fish = Commodity(package, "commodity.fish");
        var livestock = Commodity(package, "commodity.livestock");
        var draft = new OrdersDraft(state.Definition, Blue);
        var transport = Preview(package, state, draft);

        // Past the end: clamped to the row's 8 available, split by single raises.
        Assert.True(draft.SetTransportRow(transport, FishLivestock, 100));
        Assert.Equal([(fish, 4L), (livestock, 4L)], draft.Transport);
        Assert.False(draft.SetTransportRow(transport, FishLivestock, 100));

        Assert.True(draft.SetTransportRow(transport, FishLivestock, 5));
        Assert.Equal([(fish, 3L), (livestock, 2L)], draft.Transport);

        Assert.True(draft.SetTransportRow(transport, FishLivestock, -3));
        Assert.Equal([(fish, 0L), (livestock, 0L)], draft.Transport);
    }

    [Fact]
    public void SetTransportRowStopsAtTheSharedCapacity()
    {
        var (package, state) = World(capacity: 6);
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetTransport(Commodity(package, "commodity.grain"), 2);

        Assert.True(draft.SetTransportRow(Preview(package, state, draft), FishLivestock, 8));
        Assert.Equal(6, draft.Transport.Sum(static row => row.Quantity));
    }

    [Fact]
    public void ANewPairMemberJoinsItsPartnerInTheDraftOrder()
    {
        var (package, state) = World();
        var fish = Commodity(package, "commodity.fish");
        var livestock = Commodity(package, "commodity.livestock");
        var grain = Commodity(package, "commodity.grain");
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetTransport(livestock, 1);
        draft.SetTransport(grain, 1);

        Assert.True(draft.RaiseTransportRow(Preview(package, state, draft), FishLivestock));

        Assert.Equal([(livestock, 1L), (fish, 1L), (grain, 1L)], draft.Transport);
    }

    [Fact]
    public void ThePreviewLeavesTheOpeningFillAsTransportDefaultMadeIt()
    {
        var (package, state) = World();
        var draft = new OrdersDraft(state, Blue);
        var opening = TransportDefault.Create(state, Blue)!.ToArray();
        Assert.Equal(opening, draft.Transport);

        var transport = Preview(package, state, draft);

        Assert.Equal(opening, draft.Transport);
        var inputs = TransportDefault.Inputs(state, Blue);
        var line = TransportDefault.DemandLine(inputs);
        Assert.All(transport.Rows, row => Assert.Equal(
            row.Members.Sum(member => line.Demand[member.Commodity.Value]), row.Demand));
        Assert.All(transport.Rows, row => Assert.Equal(
            row.Members.Sum(member => draft.Transport.Where(entry => entry.Commodity == member.Commodity).Sum(static entry => entry.Quantity)),
            row.Requested));
    }

    [Fact]
    public void PaperCannedFoodAndArmamentsHaveNoRowAndTheirDraftEntriesAreUntouched()
    {
        var (package, state) = World();
        var draft = new OrdersDraft(state.Definition, Blue);
        var paper = Commodity(package, "commodity.paper");
        draft.SetTransport(paper, 2);
        var transport = Preview(package, state, draft);

        var shown = transport.Rows.SelectMany(static row => row.Members).Select(static member => member.CommodityKey).ToArray();
        Assert.DoesNotContain("commodity.paper", shown);
        Assert.DoesNotContain("commodity.canned-food", shown);
        Assert.DoesNotContain("commodity.armaments", shown);
        Assert.DoesNotContain("commodity.paper", transport.UnshownCommodityKeys);

        foreach (var row in transport.Rows)
        {
            _ = draft.SetTransportRow(transport, row.RowKey, 0);
        }

        Assert.Equal(2, draft.Transport.Single(entry => entry.Commodity == paper).Quantity);
        // The old screen's per-commodity sliders still list it.
        Assert.Contains(transport.Sliders, static slider => slider.CommodityKey == "commodity.paper");
    }

    [Fact]
    public void RowsSitOnTheOriginalsStripsAndOneUnstrippedGatherableTakesTheEmptySlot()
    {
        var (package, state) = World();

        var transport = Preview(package, state, new OrdersDraft(state.Definition, Blue));
        var places = transport.Rows.ToDictionary(static row => row.RowKey, static row => (row.Column, row.Slot, row.Strip));

        Assert.Equal((TransportLedgerColumn.Left, 0, (int?)4001), places[FishLivestock]);
        Assert.Equal((TransportLedgerColumn.Left, 2, (int?)4003), places["commodity.grain"]);
        Assert.Equal((TransportLedgerColumn.Left, 3, (int?)4004), places["commodity.timber"]);
        Assert.Equal((TransportLedgerColumn.Left, 4, (int?)4005), places["commodity.lumber"]);
        Assert.Equal((TransportLedgerColumn.Left, 8, (int?)4009), places["commodity.steel"]);
        Assert.Equal((TransportLedgerColumn.Right, 0, (int?)4011), places[CottonWool]);

        // Rubber and spice have no strip and both are gathered: the lower id takes k=8,
        // the other is listed as not shown.
        Assert.Equal((TransportLedgerColumn.Right, TransportLedgerLayout.UnstrippedSlot, (int?)null), places["commodity.rubber"]);
        Assert.False(places.ContainsKey("commodity.spice"));
        Assert.Equal(["commodity.spice"], transport.UnshownCommodityKeys);

        // Page order: left k ascending, then right k ascending.
        Assert.Equal(
            transport.Rows.OrderBy(static row => row.Column).ThenBy(static row => row.Slot).Select(static row => row.RowKey),
            transport.Rows.Select(static row => row.RowKey));
    }

    [Fact]
    public void AStrippedRowWithNothingAvailableIsShownGreyedAndCannotBeRaised()
    {
        var (package, state) = World();

        var steel = Preview(package, state, new OrdersDraft(state.Definition, Blue)).GetRow("commodity.steel")!;

        Assert.Equal(0, steel.Available);
        Assert.False(steel.IsActive);
        Assert.False(steel.CanRaise);
        Assert.False(steel.CanLower);
    }

    [Fact]
    public void TheUnderlineIsGreenOnlyWhenWhatIsCarriedCoversDemand()
    {
        // Seven workers on grain and an empty warehouse: grain's demand is the 2 gathered.
        var (package, state) = World(capacity: 40, stocked: false);
        var grain = Commodity(package, "commodity.grain");
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetTransport(grain, 1);

        var short1 = Preview(package, state, draft).GetRow("commodity.grain")!;
        Assert.Equal((2L, 1L), (short1.Demand, short1.Committed));
        Assert.False(short1.IsDemandMet);

        draft.SetTransport(grain, 2);
        Assert.True(Preview(package, state, draft).GetRow("commodity.grain")!.IsDemandMet);

        // A row with no demand is met at zero.
        Assert.True(Preview(package, state, draft).GetRow(CottonWool)!.IsDemandMet);
    }

    [Fact]
    public void AnUnknownRowKeyIsAScreensBug()
    {
        var (package, state) = World();
        var draft = new OrdersDraft(state.Definition, Blue);
        var transport = Preview(package, state, draft);

        Assert.Throws<ArgumentException>(() => draft.RaiseTransportRow(transport, "commodity.paper"));
        Assert.Throws<ArgumentException>(() => draft.LowerTransportRow(transport, "commodity.fish"));
    }

    private static TransportPreview Preview(CompiledWorldPackage package, WorldState state, OrdersDraft draft) =>
        OrdersPreview.Create(package, Scenario, state, draft).Transport;

    /// <summary>
    /// <see cref="OrdersFixture"/> with the shared strips' commodities, the three no
    /// strip shows, and two unstripped gatherables, all yielding on Blue's one cell:
    /// fish 4, livestock 4, cotton 3, wool 2, rubber 1, spice 1.
    /// </summary>
    private static (CompiledWorldPackage Package, WorldState State) World(
        Action<WorldContentDocument>? customise = null, long capacity = 40, bool stocked = true)
    {
        var package = OrdersFixture.Compile(document =>
        {
            string[] added = ["fish", "livestock", "cotton", "wool", "paper", "armaments", "rubber", "spice"];
            document.Commodities =
            [
                .. document.Commodities,
                .. added.Select(static key => new CommodityContentDefinition
                {
                    Key = $"commodity.{key}",
                    Name = char.ToUpperInvariant(key[0]) + key[1..],
                    Category = CommodityCategory.Raw,
                }),
            ];
            (string Key, long Yield)[] deposits = [("fish", 4), ("livestock", 4), ("cotton", 3), ("wool", 2), ("rubber", 1), ("spice", 1)];
            document.Resources =
            [
                .. document.Resources,
                .. deposits.Select(static deposit => new ResourceContentDefinition
                {
                    Key = $"resource.{deposit.Key}",
                    Commodity = $"commodity.{deposit.Key}",
                    YieldByDevelopmentLevel = [deposit.Yield],
                }),
            ];
            document.Map.Cells[0].Resources =
            [
                .. document.Map.Cells[0].Resources,
                .. deposits.Select(static deposit => $"resource.{deposit.Key}"),
            ];
            foreach (var scenario in document.Scenarios)
            {
                if (!stocked)
                {
                    scenario.InitialInventory = [];
                }

                scenario.TransportCapacity =
                [
                    new TransportCapacityContent { Country = "country.blue", Capacity = capacity },
                    new TransportCapacityContent { Country = "country.red", Capacity = 40 },
                ];
            }

            customise?.Invoke(document);
        });

        return (package, NewState(package));
    }
}
