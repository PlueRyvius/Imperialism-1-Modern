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

        if (document.FormatVersion == 5)
        {
            MigrateVersionFiveToSix(document);
        }

        if (document.FormatVersion == 6)
        {
            MigrateVersionSixToSeven(document);
        }

        if (document.FormatVersion == 7)
        {
            MigrateVersionSevenToEight(document);
        }

        if (document.FormatVersion == 8)
        {
            MigrateVersionEightToNine(document);
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
    /// entry and the improved levels rise linearly from it. Behaviour at level
    /// zero is therefore unchanged, which is the only level a version 4 world
    /// could express.
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

            // Linear, matching the manual's Resource Development Table: a
            // cultivated tile runs 1, 2, 3, 4 rather than doubling. Level zero
            // keeps the rate the version 4 package actually authored, and every
            // cell in one is undeveloped, so nothing observable changes.
            var flat = resource.YieldPerTurn;
            resource.YieldByDevelopmentLevel =
                [flat, checked(flat * 2), checked(flat * 3), checked(flat * 4)];
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

    /// <summary>
    /// Version 6 adds ports and the fishing they do. Neither can be invented for
    /// an older package: it has no port records, and nothing in a version 5
    /// document says which of its commodities is fish. So the migration adds
    /// nothing and an upgraded world simply has no ports and no fishing, which
    /// is exactly how it behaved before.
    /// </summary>
    private static void MigrateVersionFiveToSix(WorldContentDocument document)
    {
        if (document.Extraction?.PortFishing is not null)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 5 cannot contain version 6 port fishing.");
        }

        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario?.Ports is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 5 cannot contain version 6 ports.");
            }
        }

        document.FormatVersion = 6;
    }

    /// <summary>
    /// Version 7 adds rail depots, and with them the real collection model:
    /// gathering happens at connected depots, ports and the capital rather than
    /// anywhere the rail network reaches.
    /// </summary>
    /// <remarks>
    /// A version 6 package has no depot records and none can be invented, so the
    /// migration adds none. **That changes behaviour**: such a package now
    /// gathers only around its capital and its ports, where before every cell of
    /// its rail network gathered. This is the correct model rather than a
    /// regression, but it is a visible change for hand-authored worlds and for
    /// the viewer's demo package, so it is stated here rather than discovered.
    /// </remarks>
    private static void MigrateVersionSixToSeven(WorldContentDocument document)
    {
        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario?.Depots is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 6 cannot contain version 7 depots.");
            }
        }

        document.FormatVersion = 7;
    }

    /// <summary>
    /// Version 8 adds the workforce and what it eats. A version 7 package has
    /// neither, and neither can be invented: nothing in it says which of its
    /// commodities are food. So it migrates to a world whose workers never eat,
    /// which is exactly how it behaved before.
    /// </summary>
    private static void MigrateVersionSevenToEight(WorldContentDocument document)
    {
        if (document.Feeding is not null)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 7 cannot contain version 8 feeding settings.");
        }

        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario?.Workers is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 7 cannot contain version 8 workers.");
            }
        }

        document.FormatVersion = 8;
    }

    /// <summary>
    /// Version 9 prices a recipe's labour. A version 8 package cannot state one,
    /// so the migration derives it as the recipe's total input units — the rate
    /// the manual gives for the one recipe it prices outright, and the same
    /// number as "two labour per unit of output" for every recipe the original
    /// ships. See <c>docs/formulas/production.md</c>.
    /// </summary>
    /// <remarks>
    /// **This changes behaviour** for any version 8 package that also defines
    /// feeding: its production is now capped by the workforce, where before the
    /// labour pool was computed and never spent. A package with no feeding is
    /// unaffected, because labour does not bind without a workforce.
    /// </remarks>
    private static void MigrateVersionEightToNine(WorldContentDocument document)
    {
        foreach (var recipe in document.ProductionRecipes ?? [])
        {
            if (recipe is null)
            {
                continue;
            }

            if (recipe.LabourCost != 0)
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 8 cannot contain a version 9 labour cost.");
            }

            var labour = 0L;
            foreach (var input in recipe.Inputs ?? [])
            {
                labour = checked(labour + (input?.Quantity ?? 0));
            }

            recipe.LabourCost = labour;
        }

        document.FormatVersion = 9;
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
