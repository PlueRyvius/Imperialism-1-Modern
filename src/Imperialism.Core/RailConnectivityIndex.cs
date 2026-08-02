namespace Imperialism.Core;

/// <summary>
/// A packed, immutable snapshot of one country's currently usable rail topology.
/// Only rail links whose two province cells are owned by the country participate.
/// </summary>
public sealed class RailConnectivityIndex
{
    private readonly MapDimensions _dimensions;
    private readonly int[] _componentByCell;
    private readonly int[] _componentSizes;

    private RailConnectivityIndex(
        MapDimensions dimensions,
        CountryId country,
        int railLinkCount,
        int railCellCount,
        int[] componentByCell,
        int[] componentSizes)
    {
        _dimensions = dimensions;
        Country = country;
        RailLinkCount = railLinkCount;
        RailCellCount = railCellCount;
        _componentByCell = componentByCell;
        _componentSizes = componentSizes;
    }

    public CountryId Country { get; }

    public int RailLinkCount { get; }

    public int RailCellCount { get; }

    public int ComponentCount => _componentSizes.Length;

    /// <summary>
    /// Returns a stable component identifier ordered by the lowest cell index in
    /// each component, or <see langword="null"/> when the cell is not on a usable rail.
    /// </summary>
    public int? GetComponentId(CellIndex cell)
    {
        ValidateCell(cell);
        var component = _componentByCell[cell.Value];
        return component >= 0 ? component : null;
    }

    public int GetComponentSize(int componentId) =>
        (uint)componentId < (uint)_componentSizes.Length
            ? _componentSizes[componentId]
            : throw new ArgumentOutOfRangeException(nameof(componentId));

    public bool AreConnected(CellIndex first, CellIndex second)
    {
        ValidateCell(first);
        ValidateCell(second);
        var component = _componentByCell[first.Value];
        return component >= 0 && component == _componentByCell[second.Value];
    }

    internal static RailConnectivityIndex Create(
        MapDefinition map,
        IReadOnlyList<CountryId?> provinceOwners,
        IEnumerable<CellLink> railLinks,
        CountryId country)
    {
        var cellCount = map.Dimensions.CellCount;
        var parents = new int[cellCount];
        var treeSizes = new int[cellCount];
        Array.Fill(parents, -1);
        var activeLinkCount = 0;
        var activeCellCount = 0;

        foreach (var link in railLinks)
        {
            if (!IsOwnedProvinceCell(map, provinceOwners, link.First, country) ||
                !IsOwnedProvinceCell(map, provinceOwners, link.Second, country))
            {
                continue;
            }

            Activate(link.First.Value, parents, treeSizes, ref activeCellCount);
            Activate(link.Second.Value, parents, treeSizes, ref activeCellCount);
            Union(link.First.Value, link.Second.Value, parents, treeSizes);
            activeLinkCount++;
        }

        var componentByCell = new int[cellCount];
        var componentByRoot = new int[cellCount];
        Array.Fill(componentByCell, -1);
        Array.Fill(componentByRoot, -1);
        var componentSizes = new int[activeCellCount];
        var componentCount = 0;

        for (var cell = 0; cell < cellCount; cell++)
        {
            if (parents[cell] < 0)
            {
                continue;
            }

            var root = Find(cell, parents);
            var component = componentByRoot[root];
            if (component < 0)
            {
                component = componentCount++;
                componentByRoot[root] = component;
            }

            componentByCell[cell] = component;
            componentSizes[component]++;
        }

        Array.Resize(ref componentSizes, componentCount);
        return new RailConnectivityIndex(
            map.Dimensions,
            country,
            activeLinkCount,
            activeCellCount,
            componentByCell,
            componentSizes);
    }

    private static bool IsOwnedProvinceCell(
        MapDefinition map,
        IReadOnlyList<CountryId?> provinceOwners,
        CellIndex cell,
        CountryId country)
    {
        var region = map[cell].Region;
        return region.Kind == CellRegionKind.Province &&
            provinceOwners[region.Value] == country;
    }

    private static void Activate(
        int cell,
        int[] parents,
        int[] treeSizes,
        ref int activeCellCount)
    {
        if (parents[cell] >= 0)
        {
            return;
        }

        parents[cell] = cell;
        treeSizes[cell] = 1;
        activeCellCount++;
    }

    private static int Find(int cell, int[] parents)
    {
        var root = cell;
        while (parents[root] != root)
        {
            root = parents[root];
        }

        while (parents[cell] != cell)
        {
            var parent = parents[cell];
            parents[cell] = root;
            cell = parent;
        }

        return root;
    }

    private static void Union(int first, int second, int[] parents, int[] treeSizes)
    {
        var firstRoot = Find(first, parents);
        var secondRoot = Find(second, parents);
        if (firstRoot == secondRoot)
        {
            return;
        }

        if (treeSizes[firstRoot] < treeSizes[secondRoot])
        {
            (firstRoot, secondRoot) = (secondRoot, firstRoot);
        }

        parents[secondRoot] = firstRoot;
        treeSizes[firstRoot] += treeSizes[secondRoot];
    }

    private void ValidateCell(CellIndex cell)
    {
        if (!_dimensions.Contains(cell))
        {
            throw new ArgumentOutOfRangeException(nameof(cell));
        }
    }
}
