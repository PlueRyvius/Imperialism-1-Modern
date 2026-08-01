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

    internal void Validate(MapDimensions dimensions, string description)
    {
        if (!dimensions.Contains(First) || !dimensions.Contains(Second))
        {
            throw new ArgumentException(
                $"{description} link {First}-{Second} refers to a cell outside the map.");
        }

        if (dimensions.GetCoordinate(First).DistanceTo(dimensions.GetCoordinate(Second)) != 1)
        {
            throw new ArgumentException(
                $"{description} link {First}-{Second} does not join adjacent hexes.");
        }
    }
}
