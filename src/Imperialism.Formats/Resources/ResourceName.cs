using System.Globalization;

namespace Imperialism.Formats.Resources;

/// <summary>
/// A resource type or name, which Windows allows to be either a small integer or
/// a string. The original archives use both: the type is always the integer
/// <c>RT_BITMAP</c>, while the individual images carry string names such as
/// <c>10000.BMP</c>. Treating a name as an integer would silently drop every
/// image in the archive, so both readings are kept distinguishable.
/// </summary>
public readonly record struct ResourceName
{
    private ResourceName(int? id, string? text)
    {
        Id = id;
        Text = text;
    }

    /// <summary>The integer identifier, or null when this resource carries a string name.</summary>
    public int? Id { get; }

    /// <summary>The string name, or null when this resource carries an integer identifier.</summary>
    public string? Text { get; }

    public static ResourceName FromId(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        return new ResourceName(id, null);
    }

    public static ResourceName FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new ResourceName(null, text);
    }

    public bool Matches(int id) => Id == id;

    public override string ToString() => Text ??
        (Id?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
}
