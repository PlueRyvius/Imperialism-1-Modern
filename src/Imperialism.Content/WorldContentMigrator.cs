namespace Imperialism.Content;

internal static class WorldContentMigrator
{
    /// <summary>
    /// What version 4 gave every deposit before yield became a curve. Only the
    /// version 3 to 4 step uses it; version 5 replaces it outright.
    /// </summary>
    private const long VersionFourFlatYield = 1;


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

        if (document.FormatVersion == 3)
        {
            MigrateVersionThreeToFour(document);
        }

        if (document.FormatVersion == 4)
        {
            MigrateVersionFourToFive(document);
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

    /// <summary>
    /// Version 4 gives every deposit an explicit per-turn yield and states the
    /// gathering catchment. Both defaults describe an undeveloped cell in the
    /// original — one unit, gathered from a tile on or within one tile of a
    /// connected collection point — so a migrated package keeps behaving as its
    /// author intended rather than silently producing nothing. See
    /// <c>docs/formulas/extraction.md</c>.
    /// </summary>
    private static void MigrateVersionThreeToFour(WorldContentDocument document)
    {
        if (document.Resources is null)
        {
            throw new ContentValidationException("resources", "Array cannot be null.");
        }

        if (document.Extraction is not null)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 3 cannot contain version 4 extraction settings.");
        }

        for (var index = 0; index < document.Resources.Length; index++)
        {
            var resource = document.Resources[index] ??
                throw new ContentValidationException($"resources[{index}]", "Value cannot be null.");
            if (resource.YieldPerTurn != 0)
            {
                throw new ContentValidationException(
                    $"resources[{index}].yieldPerTurn",
                    "Version 3 cannot contain a version 4 yield.");
            }

            resource.YieldPerTurn = VersionFourFlatYield;
        }

        document.Extraction = new ExtractionContentSettings
        {
            CatchmentRadius = WorldContentCodec.DefaultCatchmentRadius,
        };
        document.FormatVersion = 4;
    }

    /// <summary>
    /// Version 5 makes yield a function of the cell's development level rather
    /// than one flat number. A version 4 package only knew the flat rate, and
    /// every cell in one is undeveloped, so that rate becomes the level-zero
    /// entry and the improved levels double from it. Behaviour at level zero is
    /// therefore unchanged, which is the only level a version 4 world could
    /// express.
    /// </summary>
    private static void MigrateVersionFourToFive(WorldContentDocument document)
    {
        if (document.Resources is null)
        {
            throw new ContentValidationException("resources", "Array cannot be null.");
        }

        if (document.Technologies is { Length: > 0 })
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 4 cannot contain version 5 technologies.");
        }

        for (var index = 0; index < document.Resources.Length; index++)
        {
            var resource = document.Resources[index] ??
                throw new ContentValidationException($"resources[{index}]", "Value cannot be null.");
            if (resource.YieldByDevelopmentLevel is { Length: > 0 })
            {
                throw new ContentValidationException(
                    $"resources[{index}].yieldByDevelopmentLevel",
                    "Version 4 cannot contain a version 5 yield curve.");
            }

            if (resource.RequiredTechnology is not null)
            {
                throw new ContentValidationException(
                    $"resources[{index}].requiredTechnology",
                    "Version 4 cannot contain a version 5 technology requirement.");
            }

            if (resource.YieldPerTurn <= 0)
            {
                throw new ContentValidationException(
                    $"resources[{index}].yieldPerTurn",
                    "Version 4 requires a positive yield.");
            }

            var flat = resource.YieldPerTurn;
            resource.YieldByDevelopmentLevel =
                [flat, checked(flat * 2), checked(flat * 4), checked(flat * 8)];
            resource.YieldPerTurn = 0;
        }

        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario is null)
            {
                continue;
            }

            if (scenario.CellDevelopment is { Length: > 0 } ||
                scenario.CountryTechnologies is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 4 cannot contain version 5 development or technology state.");
            }
        }

        document.FormatVersion = 5;
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
