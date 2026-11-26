using Imperialism.Content;
using Imperialism.Core;
using Xunit;

namespace Imperialism.Presentation.Tests;

public sealed class MilitaryEliminationPreviewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnEliminatedCountrysSurvivingRegimentDoesNotPreviewAsAnAcceptedMarch(bool destinationList)
    {
        var document = TurnReportFixture.CreateDocument();
        document.ArmyTypes =
        [
            new ArmyTypeContentDefinition
            {
                Key = "army.regulars", Name = "Regulars", Category = ArmyCategory.RegularInfantry, Era = 1,
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
        };
        foreach (var scenario in document.Scenarios)
        {
            scenario.ProvinceOwners =
            [
                .. scenario.ProvinceOwners,
                new ProvinceOwnerContent { Province = "province.west", Country = "country.blue" },
                new ProvinceOwnerContent { Province = "province.east", Country = "country.blue" },
            ];
        }

        var package = WorldContentCompiler.CompilePackage(document);
        var world = package.GetWorld("scenario.demo");
        var state = new WorldState(world);
        var blue = new CountryId(0);
        var red = new CountryId(1);
        var home = new ProvinceId(0);
        var west = new ProvinceId(2);
        var east = new ProvinceId(3);
        var survivor = state.CreateRegiment(blue, east, new ArmyTypeId(0));
        var invader = state.CreateRegiment(red, new ProvinceId(1), new ArmyTypeId(0));

        // Declare war, then take Blue's undefended capital on the following turn.
        TurnResolver.Resolve(state, new TurnOrders([
            new CountryTurnOrders(blue), new CountryTurnOrders(red, declareWar: [blue])]), 1);
        var capture = TurnResolver.Resolve(state, new TurnOrders([
            new CountryTurnOrders(blue),
            new CountryTurnOrders(red, regimentMoves: [new RegimentMoveOrder([invader], home)])]), 2);
        Assert.Contains(capture.Events, item => item is CountryEliminatedEvent { Country.Value: 0 });
        Assert.True(state.IsEliminated(blue));

        var draft = new OrdersDraft(world, blue);
        draft.SetRegimentMove([survivor], west);
        var preview = OrdersPreview.Create(package, "scenario.demo", state, draft);
        var targets = OrdersPreview.CreateArmyMoves(package, "scenario.demo", state, draft, [survivor]);
        var resolution = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 3);
        Assert.Contains(resolution.Events, item => item is EliminatedCountryOrdersIgnoredEvent { Country.Value: 0 });
        Assert.DoesNotContain(resolution.Events, item => item is RegimentsMovedEvent { Country.Value: 0 });
        Assert.Equal(east, state.GetRegiment(survivor)!.Province);
        Assert.True(preview.Diplomacy.OrdersSetAside);

        // The new army preview must also tell its caller that this cannot happen.
        var warning = destinationList
            ? targets.Destinations.Single(item => item.Province == west).Warning
            : Assert.Single(preview.ArmyMoves.Moves).Warning;
        Assert.NotNull(warning);
    }
}
