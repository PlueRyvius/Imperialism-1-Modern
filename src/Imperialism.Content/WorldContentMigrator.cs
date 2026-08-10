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

        if (document.FormatVersion == 9)
        {
            MigrateVersionNineToTen(document);
        }

        if (document.FormatVersion == 10)
        {
            MigrateVersionTenToEleven(document);
        }

        if (document.FormatVersion == 11)
        {
            MigrateVersionElevenToTwelve(document);
        }

        if (document.FormatVersion == 12)
        {
            MigrateVersionTwelveToThirteen(document);
        }

        if (document.FormatVersion == 13)
        {
            MigrateVersionThirteenToFourteen(document);
        }

        if (document.FormatVersion == 14)
        {
            MigrateVersionFourteenToFifteen(document);
        }

        if (document.FormatVersion == 15)
        {
            MigrateVersionFifteenToSixteen(document);
        }

        if (document.FormatVersion == 16)
        {
            MigrateVersionSixteenToSeventeen(document);
        }

        if (document.FormatVersion == 17)
        {
            MigrateVersionSeventeenToEighteen(document);
        }

        if (document.FormatVersion == 18)
        {
            MigrateVersionEighteenToNineteen(document);
        }

        if (document.FormatVersion == 19)
        {
            MigrateVersionNineteenToTwenty(document);
        }

        if (document.FormatVersion == 20)
        {
            MigrateVersionTwentyToTwentyOne(document);
        }

        if (document.FormatVersion == 21)
        {
            MigrateVersionTwentyOneToTwentyTwo(document);
        }

        if (document.FormatVersion == 22)
        {
            MigrateVersionTwentyTwoToTwentyThree(document);
        }

        if (document.FormatVersion == 23)
        {
            MigrateVersionTwentyThreeToTwentyFour(document);
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
    /// Version 9 prices a recipe's labour. A version 8 package cannot state one, so the
    /// migration derives it as the recipe's total input units.
    /// </summary>
    /// <remarks>
    /// <b>That derivation is kept even though the rule behind it is retracted.</b> The
    /// original charges two labour per cycle flat, so the importer emits two — but a
    /// version 8 package may hold any recipes at all, and the input total is the only
    /// thing derivable from one of them. Reading a modded package's economy as the
    /// original's would be a worse guess than the arithmetic it already implies.
    /// </remarks>
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

    /// <summary>
    /// Version 10 adds the fair start a skirmish runs on: what a power begins
    /// with when the scenario says nothing. A version 9 package has no such
    /// block and none can be invented for it — the baseline is a property of
    /// the original's rules, not of an arbitrary world — so it migrates to a
    /// world with no defaults and no country claiming them, which is exactly
    /// how it behaved before.
    /// </summary>
    private static void MigrateVersionNineToTen(WorldContentDocument document)
    {
        if (document.StartingDefaults is not null)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 9 cannot contain version 10 starting defaults.");
        }

        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario?.DefaultStartCountries is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 9 cannot contain version 10 default-start countries.");
            }
        }

        document.FormatVersion = 10;
    }

    /// <summary>
    /// Version 11 lets a facility be built larger: a per-facility capacity
    /// ladder and a world-level cost per point. A version 10 package has
    /// neither, and inventing them would be inventing a rule rather than
    /// filling in a value, so it migrates to a world whose industry can never
    /// grow — which is exactly how it behaved before.
    /// </summary>
    private static void MigrateVersionTenToEleven(WorldContentDocument document)
    {
        if (document.ExpansionCostPerCapacityPoint is { Length: > 0 })
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 10 cannot contain a version 11 expansion cost.");
        }

        foreach (var facility in document.ProductionFacilities ?? [])
        {
            if (facility?.CapacityLadder is not null)
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 10 cannot contain a version 11 capacity ladder.");
            }
        }

        document.FormatVersion = 11;
    }

    /// <summary>
    /// Version 12 lets a country recruit workers. A version 11 package says
    /// nothing about the Capitol's terms, and the price of a worker is a number
    /// nobody has measured — so it migrates to a world that cannot recruit,
    /// which is how it behaved before.
    /// </summary>
    private static void MigrateVersionElevenToTwelve(WorldContentDocument document)
    {
        if (document.Migration is not null)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 11 cannot contain version 12 migration settings.");
        }

        document.FormatVersion = 12;
    }

    /// <summary>
    /// Version 13 gives terrain attributes and the world civilian units, which
    /// together are the first thing in this engine able to raise a cell's
    /// development level.
    /// </summary>
    /// <remarks>
    /// A version 12 terrain was a bare key with nothing to ask about it, so
    /// every migrated terrain becomes unimprovable and the world gets no
    /// civilians. That is not a guess standing in for a missing value: a
    /// version 12 world had no way to improve anything, and this reproduces it
    /// exactly. A package that wants improvement declares it.
    /// </remarks>
    private static void MigrateVersionTwelveToThirteen(WorldContentDocument document)
    {
        if (document.Terrains is { Length: > 0 })
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 12 cannot contain version 13 terrain definitions.");
        }

        if (document.CivilianTypes is { Length: > 0 })
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 12 cannot contain version 13 civilian types.");
        }

        foreach (var resource in document.Resources ?? [])
        {
            if (resource?.ImprovedBy is not null)
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 12 cannot say which civilian improves a deposit.");
            }
        }

        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario?.Civilians is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 12 cannot contain version 13 civilians.");
            }
        }

        if (document.TerrainKeys is not { } keys)
        {
            throw new ContentValidationException("terrainKeys", "Version 12 requires an array.");
        }

        document.Terrains = keys.Select((key, index) => new TerrainContentDefinition
        {
            Key = key ?? throw new ContentValidationException(
                $"terrainKeys[{index}]", "Value cannot be null."),
            Name = CreateDisplayName(key),
            IsImprovable = false,
        }).ToArray();
        document.TerrainKeys = null;
        document.FormatVersion = 13;
    }

    /// <summary>
    /// Version 14 hides the five deposits a Prospector has to find, and says
    /// which ground is worth searching.
    /// </summary>
    /// <remarks>
    /// A version 13 package declares no prospectable terrain and no hidden
    /// deposit, and neither can be invented for it: which of an arbitrary
    /// world's terrains might conceal something is a property of that world, not
    /// a default. So it migrates to a world where nothing is hidden and no
    /// civilian searches — every deposit visible from turn one, which is exactly
    /// how it behaved before. A package that wants discovery declares it.
    /// </remarks>
    private static void MigrateVersionThirteenToFourteen(WorldContentDocument document)
    {
        foreach (var terrain in document.Terrains ?? [])
        {
            if (terrain?.Prospecting is not null)
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 13 cannot contain version 14 prospecting terms.");
            }
        }

        foreach (var resource in document.Resources ?? [])
        {
            if (resource?.RequiresDiscovery == true)
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 13 cannot hide a deposit behind discovery.");
            }
        }

        foreach (var type in document.CivilianTypes ?? [])
        {
            if (type?.Work is not (null or Imperialism.Core.CivilianWorkKind.Improve))
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 13 cannot contain a version 14 civilian work kind.");
            }
        }

        document.FormatVersion = 14;
    }

    /// <summary>
    /// Version 15 gates improvement behind technology, and gives the fair start
    /// the knowledge it begins with.
    /// </summary>
    /// <remarks>
    /// A version 14 package names no technology per level and no starting
    /// technology, and neither can be invented: which of an arbitrary world's
    /// technologies gates which rung is a property of that world, and the 1997
    /// answer — High Pressure Steam Engine and Seed Drill — is a fact about the
    /// original's rules rather than a sensible default for anything else. So it
    /// migrates to a world where every rung is ungated and no country starts
    /// knowing anything, which is exactly how it behaved before.
    /// </remarks>
    private static void MigrateVersionFourteenToFifteen(WorldContentDocument document)
    {
        foreach (var resource in document.Resources ?? [])
        {
            if (resource?.TechnologyByDevelopmentLevel is not null)
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 14 cannot gate a development level behind technology.");
            }
        }

        if (document.StartingDefaults?.Technologies is { Length: > 0 })
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 14 cannot contain version 15 starting technologies.");
        }

        document.FormatVersion = 15;
    }

    /// <summary>
    /// Version 16 limits how much a network can carry in a turn, and prices the
    /// railyard that raises it.
    /// </summary>
    /// <remarks>
    /// A version 15 package has no limit, and inventing one would be inventing
    /// the constraint rather than filling in a value — the capacity that suits a
    /// world depends entirely on how much its land yields. So it migrates to a
    /// world whose network carries everything it gathers, which is exactly how
    /// it behaved. A package that wants scarcity declares it.
    /// </remarks>
    private static void MigrateVersionFifteenToSixteen(WorldContentDocument document)
    {
        if (document.Transport is not null)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 15 cannot contain version 16 transport settings.");
        }

        if (document.StartingDefaults?.TransportCapacity is not null)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 15 cannot contain a version 16 starting transport capacity.");
        }

        if (document.StartingDefaults?.Inventory is { Length: > 0 })
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 15 cannot contain a version 16 starting stockpile.");
        }

        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario?.TransportCapacity is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 15 cannot contain version 16 transport capacity.");
            }
        }

        document.FormatVersion = 16;
    }

    /// <summary>
    /// Version 17 gives a country a treasury, lets gold and gems fill it, and
    /// lets an Engineer spend it.
    /// </summary>
    /// <remarks>
    /// A version 16 package has no money at all, so it migrates to a world where
    /// nobody holds any, nothing converts, and no Engineer can build — which is
    /// exactly how it behaved. None of it can be invented: what a commodity is
    /// worth in cash is a fact about the 1997 economy rather than about worlds in
    /// general, and a starting treasury with nothing to spend it on would be
    /// noise.
    /// </remarks>
    private static void MigrateVersionSixteenToSeventeen(WorldContentDocument document)
    {
        foreach (var commodity in document.Commodities ?? [])
        {
            if (commodity?.CashPerUnit is not null)
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 16 cannot price a commodity in cash.");
            }
        }

        if (document.StartingDefaults?.Cash is not null)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 16 cannot contain a version 17 starting treasury.");
        }

        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario?.Cash is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 16 cannot contain version 17 country cash.");
            }
        }

        if (document.Construction is not null)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 16 cannot price construction.");
        }

        foreach (var terrain in document.Terrains ?? [])
        {
            if (terrain?.Rail is not null)
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 16 cannot say which terrain carries rail.");
            }
        }

        foreach (var civilian in document.CivilianTypes ?? [])
        {
            if (civilian?.Work == Imperialism.Core.CivilianWorkKind.Construct)
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 16 has no constructing civilian.");
            }
        }

        document.FormatVersion = 17;
    }

    /// <summary>
    /// Version 18 charges a civilian for the work it does.
    /// </summary>
    /// <remarks>
    /// A version 17 package prices no improvement, so it migrates to a world
    /// where raising a tile is free — which is exactly how it behaved. The
    /// prices cannot be invented: they are a fact about the 1997 economy rather
    /// than a sensible default for any world, and a world whose civilians work
    /// for nothing is a coherent world rather than a broken one.
    /// </remarks>
    private static void MigrateVersionSeventeenToEighteen(WorldContentDocument document)
    {
        if (document.Improvement is not null)
        {
            throw new ContentValidationException(
                "formatVersion",
                "Version 17 cannot price improvement.");
        }

        document.FormatVersion = 18;
    }

    /// <summary>
    /// Version 19 lets a country buy technology, and prices rail by the ground it
    /// crosses.
    /// </summary>
    /// <remarks>
    /// A version 18 package prices no technology, so it migrates to a world where
    /// nothing is for sale — knowledge still comes only from a scenario, from the
    /// fair-start default, or from a test granting it, which is exactly how it
    /// behaved. Costs, arrival years and prerequisites cannot be invented: they are
    /// facts about the 1997 table rather than sensible defaults for any world.
    /// <para>
    /// <b>Rail is the exception, and it is the first migration here that does not
    /// preserve behaviour.</b> Version 17's flat <c>construction.railCashCost</c>
    /// is dropped rather than spread across the terrains, so a migrated package
    /// lays track for nothing. That is the owner's call: the flat figure was an
    /// invention this project had already labelled unsupported, and carrying a
    /// retracted number forward would give it a longer life than it earned. A
    /// re-import supplies the real per-terrain prices. See
    /// <c>docs/formulas/engineer.md</c>.
    /// </para>
    /// </remarks>
    private static void MigrateVersionEighteenToNineteen(WorldContentDocument document)
    {
        foreach (var technology in document.Technologies ?? [])
        {
            if (technology?.Cost is not null ||
                technology?.AvailableFrom is not null ||
                technology?.Prerequisites is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 18 cannot put a technology up for sale.");
            }
        }

        foreach (var terrain in document.Terrains ?? [])
        {
            if (terrain?.Rail is { CashCost: not 0 })
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 18 prices rail flat, not per terrain.");
            }
        }

        // Read and discarded. Nulling it is what stops it being re-emitted at
        // version 19, where the field no longer means anything.
        if (document.Construction is { } construction)
        {
            construction.RailCashCost = null;
        }

        document.FormatVersion = 19;
    }

    /// <summary>
    /// Version 20 opens a world market, and gives it ships to carry on.
    /// </summary>
    /// <remarks>
    /// A version 19 package trades nothing, so it migrates to a world where nothing is
    /// tradable, no hull exists and no price ever moves — which is exactly how it behaved.
    /// None of it can be invented: what a commodity fetches is a fact about the 1897
    /// economy rather than about worlds in general, and a world whose warehouses cannot be
    /// sold from is a coherent world rather than a broken one.
    /// <para>
    /// Countries gain <c>isGreatPower</c>, which defaults to false. That is the right
    /// migration for a world with no trade in it — the flag exists only to decide who
    /// carries a cargo — and it is why the country schema could widen without a legacy
    /// carrier: a version 19 country is a key and a name, and both survive.
    /// </para>
    /// </remarks>
    private static void MigrateVersionNineteenToTwenty(WorldContentDocument document)
    {
        foreach (var commodity in document.Commodities ?? [])
        {
            if (commodity?.WorldPrice is not null || commodity?.TradeOrder is not null)
            {
                throw new ContentValidationException(
                    "formatVersion",
                    "Version 19 cannot put a commodity on the world market.");
            }
        }

        if (document.ShipTypes is { Length: > 0 })
        {
            throw new ContentValidationException(
                "formatVersion", "Version 19 has no ships.");
        }

        if (document.Trade is not null)
        {
            throw new ContentValidationException(
                "formatVersion", "Version 19 has no world market.");
        }

        if (document.StartingDefaults?.Ships is { Length: > 0 })
        {
            throw new ContentValidationException(
                "formatVersion", "Version 19 cannot start a country with a fleet.");
        }

        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario?.Ships is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion", "Version 19 cannot place a fleet.");
            }
        }

        document.FormatVersion = 20;
    }

    /// <summary>
    /// Version 21 records whether the map has an east/west seam. Old packages
    /// had no sea-zone movement graph, so they have no behaviour to preserve
    /// here and land as non-wrapping modern maps. The legacy converter supplies
    /// the original seam explicitly when it creates a new package.
    /// </summary>
    private static void MigrateVersionTwentyToTwentyOne(WorldContentDocument document)
    {
        document.FormatVersion = 21;
    }

    /// <summary>
    /// Version 22 preserves raw scenario relationship records. Older worlds had
    /// no diplomacy consumer, so an empty list is the only behaviour-preserving
    /// migration; importing an authored legacy scenario is the path that supplies
    /// its original values.
    /// </summary>
    private static void MigrateVersionTwentyOneToTwentyTwo(WorldContentDocument document)
    {
        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario?.Relations is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion", "Version 21 cannot declare relationship records.");
            }

            if (scenario is not null)
            {
                scenario.Relations = [];
            }
        }

        document.FormatVersion = 22;
    }

    /// <summary>
    /// Version 23 can carry active relation mode/token state. A version 22
    /// package had no such authored state, so standard mode, token -1 and
    /// generation zero are the exact compatibility defaults.
    /// </summary>
    private static void MigrateVersionTwentyTwoToTwentyThree(WorldContentDocument document)
    {
        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario?.RelationStates is { Length: > 0 } || scenario?.RelationSequence != 0)
            {
                throw new ContentValidationException(
                    "formatVersion", "Version 22 cannot declare active relation state.");
            }

            if (scenario is not null)
            {
                scenario.RelationSequence = 0;
                scenario.RelationStates = [];
            }
        }

        document.FormatVersion = 23;
    }

    /// <summary>
    /// Version 24 preserves original scenario army records. A version 23
    /// package had no army consumer, so an empty list is the only compatible
    /// migration; legacy import is the evidence-backed path that supplies them.
    /// </summary>
    private static void MigrateVersionTwentyThreeToTwentyFour(WorldContentDocument document)
    {
        foreach (var scenario in document.Scenarios ?? [])
        {
            if (scenario?.Armies is { Length: > 0 })
            {
                throw new ContentValidationException(
                    "formatVersion", "Version 23 cannot declare army records.");
            }

            if (scenario is not null)
            {
                scenario.Armies = [];
            }
        }

        document.FormatVersion = 24;
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
