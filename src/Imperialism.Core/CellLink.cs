namespace Imperialism.Core;

/// <summary>An undirected connection between two distinct cells.</summary>
public readonly record struct CellLink
{
    public CellLink(CellIndex first, CellIndex second)
    {
        if (first == second)
        {
            throw new ArgumentException("A cell link requires two distinct cells.", nameof(second));
        }

        if (first.Value < second.Value)
        {
            First = first;
            Second = second;
        }
        else
        {
            First = second;
            Second = first;
        }
    }

    public CellIndex First { get; }

    public CellIndex Second { get; }

    public bool Contains(CellIndex cell) => cell == First || cell == Second;

    /// <summary>
    /// Adjacency is the map's, not the plane's: on a map with an east-west seam
    /// the first and last columns touch, so a link across it is as adjacent as
    /// any other. Planar distance would call it the width of the world.
    /// </summary>
    internal void Validate(MapDefinition map, string description)
    {
        var dimensions = map.Dimensions;
        if (!dimensions.Contains(First) || !dimensions.Contains(Second))
        {
            throw new ArgumentException(
                $"{description} link {First}-{Second} refers to a cell outside the map.");
        }

        if (!map.AreAdjacent(First, Second))
        {
            throw new ArgumentException(
                $"{description} link {First}-{Second} does not join adjacent hexes.");
        }
    }
}
