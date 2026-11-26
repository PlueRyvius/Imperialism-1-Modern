using Imperialism.Formats;
using Imperialism.LegacyImport;
using Xunit;

namespace Imperialism.LegacyImport.Tests;

public sealed class MergeHullGateRegressionTests
{
    // Requirements retained from issue #54 / main, independent of the merge's table.
    [Theory]
    [InlineData("ironclad", "advanced-iron-working")]
    [InlineData("advanced-ironclad", "steel-armour-plate")]
    [InlineData("freighter", "marine-engineering")]
    [InlineData("armoured-cruiser", "marine-engineering")]
    [InlineData("dreadnought", "improved-range-finding")]
    [InlineData("battle-cruiser", "improved-range-finding")]
    public void MergeRetainsTheHullTechnologyGate(string hull, string technology)
    {
        var profile = new MapFormatProfile(2, 1, trailerRecordCount: 2, trailerRecordSize: 3);
        var map = new MapDocument(profile,
        [
            new HexCell { Terrain = 1, Province = 0, NationZoneA = 0, NationZoneB = 0 },
            new HexCell { Terrain = 0, Province = ushort.MaxValue, NationZoneA = 33, NationZoneB = 33 },
        ], new byte[profile.TrailerSize]);
        var scenario = new ScenarioDocument(
        [
            new ScenarioRecord("year", [0]),
            new ScenarioRecord("cnam", [0], "Country"),
            new ScenarioRecord("pnam", [0], "Province"),
        ]);
        var converted = LegacyWorldConverter.Convert(map, scenario, null, "merge-hull-regression");
        Assert.True(converted.Success, converted.Report.ToHumanReadable());
        Assert.Equal("technology." + technology,
            converted.Document!.ShipTypes.Single(ship => ship.Key == "ship." + hull).RequiredTechnology);
    }
}
