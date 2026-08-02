namespace Imperialism.Content;

internal static class WorldContentMigrator
{
    public static WorldContentDocument ToCurrent(WorldContentDocument document)
    {
        if (document.FormatVersion == 1)
        {
            MigrateVersionOneToTwo(document);
        }

        if (document.FormatVersion == 2)
        {
            MigrateVersionTwoToThree(document);
        }

        return document;
    }

    private static void MigrateVersionOneToTwo(WorldContentDocument document)
    {

        if (document.ResourceKeys is null)
        {
            throw new ContentValidationException("resourceKeys", "Version 1 requires an array.");
        }

        if (document.Commodities is null)
        {
            throw new ContentValidationException("commodities", "Array cannot be null.");
        }

        if (document.Resources is null)
        {
            throw new ContentValidationException("resources", "Array cannot be null.");
        }

        if (document.Commodities.Length != 0 || document.Resources.Length != 0)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 1 cannot contain version 2 commodity or resource definitions.");
        }

        var resourceKeys = new HashSet<string>(StringComparer.Ordinal);
        var commodities = new CommodityContentDefinition[document.ResourceKeys.Length];
        var resources = new ResourceContentDefinition[document.ResourceKeys.Length];
        for (var index = 0; index < document.ResourceKeys.Length; index++)
        {
            var resourceKey = document.ResourceKeys[index] ??
                throw new ContentValidationException($"resourceKeys[{index}]", "Value cannot be null.");
            if (!resourceKeys.Add(resourceKey))
            {
                throw new ContentValidationException(
                    $"resourceKeys[{index}]",
                    $"Duplicate key '{resourceKey}'.");
            }

            var commodityKey = CreateCommodityKey(resourceKey);
            commodities[index] = new CommodityContentDefinition
            {
                Key = commodityKey,
                Name = CreateDisplayName(resourceKey),
                Category = Imperialism.Core.CommodityCategory.Raw,
            };
            resources[index] = new ResourceContentDefinition
            {
                Key = resourceKey,
                Commodity = commodityKey,
            };
        }

        document.FormatVersion = 2;
        document.Commodities = commodities;
        document.Resources = resources;
        document.ResourceKeys = null;
    }

    private static void MigrateVersionTwoToThree(WorldContentDocument document)
    {
        if (document.ProductionFacilities is null || document.ProductionRecipes is null)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 2 production collections cannot be null.");
        }

        if (document.ProductionFacilities.Length != 0 || document.ProductionRecipes.Length != 0 ||
            (document.Scenarios?.Any(static scenario =>
                scenario?.ProductionCapacities is { Length: > 0 }) ?? false))
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 2 cannot contain version 3 production definitions or capacities.");
        }

        document.FormatVersion = 3;
    }

    private static string CreateCommodityKey(string resourceKey) =>
        resourceKey.StartsWith("resource.", StringComparison.Ordinal) && resourceKey.Length > 9
            ? $"commodity.{resourceKey[9..]}"
            : $"commodity/from-resource/{resourceKey}";

    private static string CreateDisplayName(string resourceKey)
    {
        var separator = Math.Max(resourceKey.LastIndexOf('.'), resourceKey.LastIndexOf('/'));
        var segment = separator >= 0 ? resourceKey[(separator + 1)..] : resourceKey;
        var words = segment.Replace('-', ' ').Replace('_', ' ');
        return words.Length == 0 ? resourceKey : char.ToUpperInvariant(words[0]) + words[1..];
    }
}
