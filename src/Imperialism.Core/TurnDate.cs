namespace Imperialism.Core;

/// <summary>A strategic turn boundary. Each turn advances one three-month quarter.</summary>
public readonly record struct TurnDate
{
    public TurnDate(int year, int quarter)
    {
        if (quarter is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(quarter), "Quarter must be between 1 and 4.");
        }

        Year = year;
        Quarter = quarter;
    }

    public int Year { get; }

    public int Quarter { get; }

    public TurnDate Next() => Quarter < 4
        ? new TurnDate(Year, Quarter + 1)
        : new TurnDate(checked(Year + 1), 1);

    public override string ToString() => $"{Year} Q{Quarter}";
}
