using System.Globalization;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>Transport's original quarter names, without changing the simulation date.</summary>
public static class TransportText
{
    public static string FormatDate(TurnDate date)
    {
        var quarter = date.Quarter switch
        {
            1 => "Spring",
            2 => "Summer",
            3 => "Fall",
            4 => "Winter",
            _ => throw new ArgumentOutOfRangeException(nameof(date)),
        };
        return quarter + ", " + date.Year.ToString(CultureInfo.InvariantCulture);
    }
}
