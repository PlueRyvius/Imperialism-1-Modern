using Imperialism.Presentation;
using Xunit;
using static Imperialism.Presentation.Tests.OrdersFixture;

namespace Imperialism.Presentation.Tests;

public sealed class IndustryWarehouseBalanceTests
{
    [Fact]
    public void WarehouseBalanceIncludesOutputAndAcceptedConstructionAndRemainsDetached()
    {
        var package = Compile();
        var state = NewState(package);
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetProduction(Recipe(package, "recipe.lumber"), 2);
        draft.SetExpansion(Facility(package, "facility.lumber-mill"), true);
        draft.BuildTransportCapacity = 1;
        draft.RecruitWorkers = 1;
        var preview = OrdersPreview.Create(package, Scenario, state, draft).Industry;
        long Balance(string key) => preview.WarehouseBalance.Single(row => row.CommodityKey == "commodity." + key).Quantity;
        // Opening timber/lumber/steel = 10 each. Two cycles consume four timber,
        // make two lumber. Expansion takes two lumber + steel; railyard one each.
        // One recruit consumes one canned food, after all construction.
        Assert.Equal(6, Balance("timber"));
        Assert.Equal(9, Balance("lumber"));
        Assert.Equal(7, Balance("steel"));
        Assert.Equal(4, Balance("canned-food"));
        Assert.Equal(10, state.GetAvailableQuantity(Blue, Commodity(package, "commodity.timber")));
        draft.SetProduction(Recipe(package, "recipe.lumber"), 0);
        Assert.Equal(6, Balance("timber"));
        Assert.Equal(10, OrdersPreview.Create(package, Scenario, state, draft).Industry.WarehouseBalance.Single(row => row.CommodityKey == "commodity.timber").Quantity);
    }
}
