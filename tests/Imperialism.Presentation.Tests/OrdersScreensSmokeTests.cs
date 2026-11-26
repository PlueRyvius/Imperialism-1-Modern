using Imperialism.Content;
using Imperialism.Core;
using Xunit;

namespace Imperialism.Presentation.Tests;

public sealed class OrdersScreensSmokeTests
{
    [Theory]
    [InlineData("scenario.opening", 0)]
    [InlineData("scenario.opening", 1)]
    [InlineData("scenario.reversal", 0)]
    [InlineData("scenario.reversal", 1)]
    public void FourScreenSmokeDraftResolvesResearchAndOfferWithoutLosingProductionOrTransport(
        string scenarioKey, int countryIndex)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Imperialism.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var path = Path.Combine(directory.FullName, "src", "Imperialism.Client", "demo", "orders.iworld");
        var package = WorldContentCodec.DecodeAndCompilePackage(File.ReadAllBytes(path));
        var state = new WorldState(package.GetWorld(scenarioKey));
        var country = new CountryId(countryIndex);
        var draft = new OrdersDraft(state.Definition, country);
        var recipe = package.Catalog.GetProductionRecipeId("recipe.lumber");
        var grain = package.Catalog.GetCommodityId("commodity.grain");
        var timber = package.Catalog.GetCommodityId("commodity.timber");
        var technology = package.Catalog.GetTechnologyId("technology.sawmill");
        draft.SetProduction(recipe, 1);
        draft.SetTransport(grain, 1);
        draft.SetTechnologyPurchase(technology, true);
        draft.SetOffer(timber, 1);

        var preview = OrdersPreview.Create(package, scenarioKey, state, draft);
        Assert.Equal((1000L, 500L, false),
            (preview.Technology.Cash, preview.Technology.CommittedCost, preview.Technology.IsOverCash));
        Assert.True(Assert.Single(preview.Technology.Offered).IsSelected);
        Assert.True(Assert.Single(preview.Technology.Purchases).IsAffordable);
        var trade = Assert.Single(preview.Trade.Rows);
        Assert.Equal((100L, 8L, 2L, 6L, 1L),
            (trade.WorldPrice, trade.Stock, trade.ClaimedByIndustry, trade.Sellable, trade.Offer));
        Assert.False(trade.IsOverOffered);

        var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events;
        var purchased = Assert.Single(events.OfType<TechnologyPurchasedEvent>());
        Assert.Equal(country, purchased.Country);
        Assert.Equal(technology, purchased.Technology);
        Assert.True(state.HasTechnology(country, technology));
        Assert.Equal(500, state.GetCash(country));
        var unsold = Assert.Single(events.OfType<TradeUnfilledEvent>());
        Assert.Equal(country, unsold.Country);
        Assert.Equal(timber, unsold.Commodity);
        Assert.Equal(1, unsold.Requested);
        Assert.Equal(0, unsold.Settled); // Every rival still submits empty orders.
        var production = Assert.Single(events.OfType<ProductionCompletedEvent>());
        Assert.Equal(recipe, production.Recipe);
        Assert.Equal(1, production.CompletedCycles);
        Assert.Contains(events.OfType<CommoditiesTransportedEvent>(),
            item => item.Country == country && item.CapacityUsed == 1);
        Assert.Equal(1, state.CompletedTurnCount);
    }
}
