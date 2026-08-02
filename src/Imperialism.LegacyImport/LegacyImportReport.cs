using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Imperialism.LegacyImport;

public enum LegacyImportSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record LegacyImportDiagnostic(
    LegacyImportSeverity Severity,
    string Code,
    string Location,
    string Message,
    int Count = 1);

public sealed class LegacyImportReport
{
    private readonly List<LegacyImportDiagnostic> _diagnostics = [];
    private readonly SortedDictionary<string, int> _deferredCounts = new(StringComparer.Ordinal);

    public IReadOnlyList<LegacyImportDiagnostic> Diagnostics => _diagnostics;

    public IReadOnlyDictionary<string, int> DeferredCounts => _deferredCounts;

    public bool HasErrors => _diagnostics.Any(static item => item.Severity == LegacyImportSeverity.Error);

    public int ErrorCount => Count(LegacyImportSeverity.Error);

    public int WarningCount => Count(LegacyImportSeverity.Warning);

    public string ToHumanReadable()
    {
        var builder = new StringBuilder();
        builder.Append("Legacy import: ")
            .Append(ErrorCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(" error(s), ")
            .Append(WarningCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .AppendLine(" warning(s)");

        foreach (var diagnostic in _diagnostics
                     .OrderBy(static item => item.Severity)
                     .ThenBy(static item => item.Code, StringComparer.Ordinal)
                     .ThenBy(static item => item.Location, StringComparer.Ordinal)
                     .ThenBy(static item => item.Message, StringComparer.Ordinal))
        {
            builder.Append(diagnostic.Severity.ToString().ToLowerInvariant())
                .Append(' ')
                .Append(diagnostic.Code)
                .Append(" [")
                .Append(diagnostic.Location)
                .Append("]: ")
                .Append(diagnostic.Message);
            if (diagnostic.Count != 1)
            {
                builder.Append(" (count: ")
                    .Append(diagnostic.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append(')');
            }

            builder.AppendLine();
        }

        if (_deferredCounts.Count > 0)
        {
            builder.AppendLine("Deferred information:");
            foreach (var (key, count) in _deferredCounts)
            {
                builder.Append("  ")
                    .Append(key)
                    .Append(": ")
                    .AppendLine(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            },
        };
        return JsonSerializer.Serialize(
            new
            {
                HasErrors,
                ErrorCount,
                WarningCount,
                DeferredCounts = _deferredCounts,
                Diagnostics = _diagnostics
                    .OrderBy(static item => item.Severity)
                    .ThenBy(static item => item.Code, StringComparer.Ordinal)
                    .ThenBy(static item => item.Location, StringComparer.Ordinal)
                    .ThenBy(static item => item.Message, StringComparer.Ordinal),
            },
            options) + "\n";
    }

    internal void Add(
        LegacyImportSeverity severity,
        string code,
        string location,
        string message,
        int count = 1)
    {
        if (count <= 0)
        {
            return;
        }

        var index = _diagnostics.FindIndex(item =>
            item.Severity == severity &&
            item.Code == code &&
            item.Location == location &&
            item.Message == message);
        if (index >= 0)
        {
            _diagnostics[index] = _diagnostics[index] with
            {
                Count = checked(_diagnostics[index].Count + count),
            };
        }
        else
        {
            _diagnostics.Add(new LegacyImportDiagnostic(severity, code, location, message, count));
        }
    }

    internal void Defer(string category, int count)
    {
        if (count <= 0)
        {
            return;
        }

        _deferredCounts[category] = checked(_deferredCounts.GetValueOrDefault(category) + count);
    }

    private int Count(LegacyImportSeverity severity) => _diagnostics
        .Where(item => item.Severity == severity)
        .Sum(static item => item.Count);
}
