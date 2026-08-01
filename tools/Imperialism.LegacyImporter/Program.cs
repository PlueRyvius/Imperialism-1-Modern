using System.Text;
using Imperialism.Content;
using Imperialism.Formats;
using Imperialism.LegacyImport;

try
{
    var options = Options.Parse(args);
    var map = LegacyMapCodec.Load(options.MapPath, MapFormatProfile.Imperialism1);
    var scenario = LegacyScenarioCodec.Load(options.ScenarioPath);
    var info = options.InfoPath is null ? null : ScenarioInfoCodec.Load(options.InfoPath);
    var result = LegacyWorldConverter.Convert(map, scenario, info, options.PackageKey);

    Console.Write(result.Report.ToHumanReadable());
    if (options.JsonReportPath is not null)
    {
        File.WriteAllText(options.JsonReportPath, result.Report.ToJson(), new UTF8Encoding(false));
    }

    if (!result.Success)
    {
        return 1;
    }

    WorldContentCodec.Save(options.OutputPath, result.Document!);
    _ = WorldContentCompiler.Compile(WorldContentCodec.Load(options.OutputPath));
    Console.WriteLine($"Wrote {Path.GetFullPath(options.OutputPath)}");
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Legacy import failed: {exception.Message}");
    return 2;
}

internal sealed record Options(
    string MapPath,
    string ScenarioPath,
    string? InfoPath,
    string OutputPath,
    string PackageKey,
    string? JsonReportPath)
{
    public static Options Parse(string[] args)
    {
        string? map = null;
        string? scenario = null;
        string? info = null;
        string? output = null;
        string? packageKey = null;
        string? report = null;
        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            var value = NextValue(args, ref index, option);
            switch (option)
            {
                case "--map":
                    map = value;
                    break;
                case "--scenario":
                    scenario = value;
                    break;
                case "--inf":
                    info = value;
                    break;
                case "--output":
                    output = value;
                    break;
                case "--package-key":
                    packageKey = value;
                    break;
                case "--report-json":
                    report = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{option}'.");
            }
        }

        if (map is null || scenario is null || output is null || packageKey is null)
        {
            throw new ArgumentException(
                "Usage: --map FILE --scenario FILE [--inf FILE] --output FILE " +
                "--package-key KEY [--report-json FILE]");
        }

        return new Options(map, scenario, info, output, packageKey, report);
    }

    private static string NextValue(string[] args, ref int index, string option)
    {
        if (!option.StartsWith("--", StringComparison.Ordinal) || ++index >= args.Length)
        {
            throw new ArgumentException($"Option '{option}' requires a value.");
        }

        return args[index];
    }
}
