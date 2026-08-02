namespace Imperialism.Formats;

public sealed class ScenarioDocument
{
    public ScenarioDocument(
        IEnumerable<ScenarioRecord>? records = null,
        ReadOnlySpan<byte> trailingBytes = default)
    {
        Records = records?.ToList() ?? [];
        TrailingBytes = trailingBytes.ToArray();
    }

    public IList<ScenarioRecord> Records { get; }

    public byte[] TrailingBytes { get; set; }
}
