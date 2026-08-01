namespace Imperialism.Content;

internal static class WorldContentMigrator
{
    public static WorldContentDocument ToCurrent(WorldContentDocument document)
    {
        if (document.FormatVersion != 1)
        {
            return document;
        }

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

        var usedCommodityKeys = new HashSet<string>(StringComparer.Ordinal);
        var commodities = new CommodityContentDefinition[document.ResourceKeys.Length];
        var resources = new ResourceContentDefinition[document.ResourceKeys.Length];
        for (var index = 0; index < document.ResourceKeys.Length; index++)
        {
            var resourceKey = document.ResourceKeys[index] ??
                throw new ContentValidationException($"resourceKeys[{index}]", "Value cannot be null.");
            var commodityKey = CreateCommodityKey(resourceKey, usedCommodityKeys);
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

        document.FormatVersion = WorldContentCodec.CurrentVersion;
        document.Commodities = commodities;
        document.Resources = resources;
        document.ResourceKeys = null;
        return document;
    }

    private static string CreateCommodityKey(string resourceKey, HashSet<string> used)
    {
        var candidate = resourceKey.StartsWith("resource.", StringComparison.Ordinal) && resourceKey.Length > 9
            ? $"commodity.{resourceKey[9..]}"
            : $"commodity/from-resource/{resourceKey}";
        if (used.Add(candidate))
        {
            return candidate;
        }

        candidate = $"commodity/from-resource/{resourceKey}";
        _ = used.Add(candidate);
        return candidate;
    }

    private static string CreateDisplayName(string resourceKey)
    {
        var separator = Math.Max(resourceKey.LastIndexOf('.'), resourceKey.LastIndexOf('/'));
        var segment = separator >= 0 ? resourceKey[(separator + 1)..] : resourceKey;
        var words = segment.Replace('-', ' ').Replace('_', ' ');
        return words.Length == 0 ? resourceKey : char.ToUpperInvariant(words[0]) + words[1..];
    }
}
