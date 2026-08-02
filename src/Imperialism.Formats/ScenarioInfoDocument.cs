namespace Imperialism.Formats;

public sealed class ScenarioInfoDocument
{
    internal const int CountrySectionCount = 7;
    internal const int MetadataValueCount = 8;

    private readonly byte[]? _rawBytes;
    private readonly string? _originalTitle;
    private readonly string? _originalOverview;
    private readonly string[]? _originalCountrySections;
    private readonly int[]? _originalMetadata;

    public ScenarioInfoDocument(
        string title,
        string overview,
        IEnumerable<string> countrySections,
        IEnumerable<int> metadata)
        : this(title, overview, countrySections, metadata, null)
    {
    }

    internal ScenarioInfoDocument(
        string title,
        string overview,
        IEnumerable<string> countrySections,
        IEnumerable<int> metadata,
        byte[]? rawBytes)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(countrySections);
        ArgumentNullException.ThrowIfNull(metadata);
        Title = title;
        Overview = overview;
        CountrySections = countrySections.ToList();
        Metadata = metadata.ToList();
        _rawBytes = rawBytes?.ToArray();
        if (_rawBytes is not null)
        {
            _originalTitle = title;
            _originalOverview = overview;
            _originalCountrySections = CountrySections.ToArray();
            _originalMetadata = Metadata.ToArray();
        }
    }

    public string Title { get; set; }

    public string Overview { get; set; }

    public IList<string> CountrySections { get; }

    public IList<int> Metadata { get; }

    public ReadOnlyMemory<byte> RawBytes => _rawBytes ?? ReadOnlyMemory<byte>.Empty;

    internal bool IsUnchanged =>
        _rawBytes is not null &&
        Title == _originalTitle &&
        Overview == _originalOverview &&
        CountrySections.SequenceEqual(_originalCountrySections!, StringComparer.Ordinal) &&
        Metadata.SequenceEqual(_originalMetadata!);
}
