namespace Imperialism.Core;

public readonly record struct RiverPath
{
    public RiverPath(RiverEndpoint first, RiverEndpoint second)
    {
        if (!Enum.IsDefined(first))
        {
            throw new ArgumentOutOfRangeException(nameof(first));
        }

        if (!Enum.IsDefined(second))
        {
            throw new ArgumentOutOfRangeException(nameof(second));
        }

        if (first == second)
        {
            throw new ArgumentException("A river path must join two distinct endpoints.", nameof(second));
        }

        if (first < second)
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

    public RiverEndpoint First { get; }

    public RiverEndpoint Second { get; }

    internal bool IsValid =>
        Enum.IsDefined(First) &&
        Enum.IsDefined(Second) &&
        First != Second;
}
