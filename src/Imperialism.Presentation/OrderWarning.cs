using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>
/// Why a drafted order would not happen as drafted: Core's own refusal of it, or Core
/// setting every order the country gave aside.
/// </summary>
/// <remarks>
/// Core's refusal enums cannot say the second thing: an eliminated country's orders are
/// replaced with empty ones before anything is planned, so no planner ever sees them and
/// the only trace is one <see cref="EliminatedCountryOrdersIgnoredEvent"/>. A preview
/// that showed such an order as accepted would promise what Core does not do.
/// </remarks>
/// <param name="Refusal">Core's refusal, or null when <paramref name="OrdersSetAside"/>.</param>
/// <param name="OrdersSetAside">
/// The drafting country is eliminated as the turn opens, so Core reads none of its
/// orders and reports <see cref="EliminatedCountryOrdersIgnoredEvent"/> instead.
/// </param>
public readonly record struct OrderWarning<TRefusal>(TRefusal? Refusal, bool OrdersSetAside)
    where TRefusal : struct, Enum
{
    /// <summary>The warning for every order of an eliminated country.</summary>
    public static OrderWarning<TRefusal> SetAside { get; } = new(null, true);

    /// <summary>Core's refusal, as a warning.</summary>
    public static implicit operator OrderWarning<TRefusal>(TRefusal refusal) => new(refusal, false);
}
