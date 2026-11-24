using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Imperialism.AssetExtractor;
using Imperialism.Formats.Resources;

try
{
    var options = Options.Parse(args);
    return options.Mode switch
    {
        RunMode.Report => Report(options),
        RunMode.Probe => Probe(options),
        RunMode.ContactSheet => ContactSheets(options),
        _ => Extract(options),
    };
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
catch (InvalidDataException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static int Report(Options options)
{
    var archives = new List<object>();
    foreach (var path in options.Archives)
    {
        var reader = new PortableExecutableResourceReader(File.ReadAllBytes(path));
        var images = new List<object>();
        var failures = new List<object>();
        foreach (var entry in reader.OfType(PortableExecutableResourceReader.BitmapResourceType))
        {
            try
            {
                images.Add(Describe(reader, entry));
            }
            catch (InvalidDataException exception)
            {
                failures.Add(new { resource = entry.Name.ToString(), reason = exception.Message });
            }
        }

        archives.Add(new
        {
            archive = Path.GetFileName(path),
            sha256 = Hash(File.ReadAllBytes(path)),
            resourceCount = reader.Entries.Count,
            bitmapCount = reader.OfType(PortableExecutableResourceReader.BitmapResourceType).Count(),
            types = reader.Entries
                .GroupBy(entry => entry.Type.ToString(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            palette = Palette(reader),
            images,
            failures,
        });
    }

    var json = JsonSerializer.Serialize(archives, new JsonSerializerOptions { WriteIndented = true });
    if (options.Output is null)
    {
        Console.WriteLine(json);
    }
    else
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Output))!);
        File.WriteAllText(options.Output, json);
        Console.Error.WriteLine($"wrote {options.Output}");
    }

    return 0;
}

static int Probe(Options options)
{
    // Nine-patch margins for hand-drawn chrome have to contain the whole bevel
    // and inlay or the light direction breaks at the seam. Reading the runs of
    // colour along an edge measures where those bands actually end, which beats
    // dragging handles in an inspector and calling the result 14.
    foreach (var path in options.Archives)
    {
        var reader = new PortableExecutableResourceReader(File.ReadAllBytes(path));
        foreach (var name in options.Probes)
        {
            var entry = reader
                .OfType(PortableExecutableResourceReader.BitmapResourceType)
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.Name.ToString(), name, StringComparison.Ordinal));
            if (entry is null)
            {
                continue;
            }

            var bitmap = DeviceIndependentBitmap.Decode(reader.GetData(entry).Span);
            Console.WriteLine($"{Path.GetFileName(path)} {name} {bitmap.Width}x{bitmap.Height}");
            Console.WriteLine($"  top    {Runs(bitmap, horizontal: true, line: 0)}");
            Console.WriteLine($"  left   {Runs(bitmap, horizontal: false, line: 0)}");
            Console.WriteLine($"  bottom {Runs(bitmap, horizontal: true, line: bitmap.Height - 1)}");
            Console.WriteLine($"  right  {Runs(bitmap, horizontal: false, line: bitmap.Width - 1)}");
            Console.WriteLine($"  columnAt8 {Runs(bitmap, horizontal: false, line: 8)}");
            Console.WriteLine($"  rowAt8 {Runs(bitmap, horizontal: true, line: 8)}");
            Console.WriteLine($"  centreColumn {Runs(bitmap, horizontal: false, line: bitmap.Width / 2)}");
            Console.WriteLine($"  centreRow {Runs(bitmap, horizontal: true, line: bitmap.Height / 2)}");
            foreach (var field in new[] { 0, 40 })
            {
                Console.WriteLine($"  fieldBox index {field}: {FieldBox(bitmap, field)}");
            }
        }
    }

    return 0;
}

/// <summary>
/// The extent of a flat interior colour, which is how a framed panel says where
/// its border stops. Reported as the margins a nine-patch would need.
/// </summary>
static string FieldBox(DeviceIndependentBitmap bitmap, int field)
{
    var left = int.MaxValue;
    var top = int.MaxValue;
    var right = -1;
    var bottom = -1;
    for (var row = 0; row < bitmap.Height; row++)
    {
        for (var column = 0; column < bitmap.Width; column++)
        {
            if (bitmap.PaletteIndices[(row * bitmap.Width) + column] != field)
            {
                continue;
            }

            left = Math.Min(left, column);
            top = Math.Min(top, row);
            right = Math.Max(right, column);
            bottom = Math.Max(bottom, row);
        }
    }

    return right < 0
        ? "absent"
        : $"x {left}..{right}, y {top}..{bottom} " +
          $"(margins left {left} top {top} right {bitmap.Width - 1 - right} bottom {bitmap.Height - 1 - bottom})";
}

static string Runs(DeviceIndependentBitmap bitmap, bool horizontal, int line)
{
    if (!bitmap.IsPalettized)
    {
        return "(direct colour)";
    }

    var length = horizontal ? bitmap.Width : bitmap.Height;
    var parts = new List<string>();
    var start = 0;
    for (var position = 1; position <= length; position++)
    {
        var previous = Sample(bitmap, horizontal, line, position - 1);
        var current = position < length ? Sample(bitmap, horizontal, line, position) : -1;
        if (current == previous)
        {
            continue;
        }

        parts.Add($"{start}..{position - 1}:{previous}");
        start = position;
        if (parts.Count >= 14)
        {
            parts.Add("...");
            break;
        }
    }

    return string.Join(" ", parts);
}

static int Sample(DeviceIndependentBitmap bitmap, bool horizontal, int line, int position) =>
    horizontal
        ? bitmap.PaletteIndices[(line * bitmap.Width) + position]
        : bitmap.PaletteIndices[(position * bitmap.Width) + line];

static string[] Palette(PortableExecutableResourceReader reader)
{
    // Every image in every archive carries the same table, so reporting the
    // first one reports all of them.
    var entry = reader.OfType(PortableExecutableResourceReader.BitmapResourceType).FirstOrDefault();
    if (entry is null)
    {
        return [];
    }

    var bitmap = DeviceIndependentBitmap.Decode(reader.GetData(entry).Span);
    return Enumerable.Range(0, bitmap.PaletteCount)
        .Select(index => PaletteColor(bitmap, index) ?? string.Empty)
        .ToArray();
}

static object Describe(PortableExecutableResourceReader reader, ResourceEntry entry)
{
    var bitmap = DeviceIndependentBitmap.Decode(reader.GetData(entry).Span);
    var histogram = new int[bitmap.IsPalettized ? bitmap.PaletteCount : 0];
    foreach (var index in bitmap.PaletteIndices)
    {
        histogram[index]++;
    }

    return new
    {
        resource = entry.Name.ToString(),
        language = entry.Language,
        width = bitmap.Width,
        height = bitmap.Height,
        bitsPerPixel = bitmap.BitsPerPixel,
        paletteCount = bitmap.PaletteCount,
        // The five commonest indices, and the colours the two conventional key
        // candidates resolve to. Together these are enough to form a hypothesis
        // about the transparency rule without extracting anything.
        commonIndices = histogram
            .Select((count, index) => new { index, count })
            .Where(pair => pair.count > 0)
            .OrderByDescending(pair => pair.count)
            .Take(5)
            .ToArray(),
        firstEntryColor = PaletteColor(bitmap, 0),
        lastEntryColor = PaletteColor(bitmap, bitmap.PaletteCount - 1),
        // Whether every image shares one palette decides whether an index key and
        // a colour key are the same statement or two different ones.
        paletteSha256 = Hash(bitmap.Palette),
        // A single index all the way round the outside is the strongest evidence
        // an image is keyed, and which index it is keyed on.
        borderIndex = UniformBorderIndex(bitmap),
        // Index 16 is magenta, the only one in the shared palette, and the
        // conventional key of the era. How many pixels use it is what separates
        // a keyed sprite from an opaque background.
        keyPixelCount = bitmap.IsPalettized ? histogram.ElementAtOrDefault(16) : 0,
        cornerIndex = bitmap.IsPalettized ? bitmap.PaletteIndices[0] : (int?)null,
        pixelSha256 = Hash(bitmap.Pixels),
    };
}

static string? PaletteColor(DeviceIndependentBitmap bitmap, int index)
{
    if (!bitmap.IsPalettized || index < 0 || index >= bitmap.PaletteCount)
    {
        return null;
    }

    return string.Create(
        CultureInfo.InvariantCulture,
        $"{bitmap.Palette[index * 4]:X2}{bitmap.Palette[(index * 4) + 1]:X2}{bitmap.Palette[(index * 4) + 2]:X2}");
}

static int? UniformBorderIndex(DeviceIndependentBitmap bitmap)
{
    if (!bitmap.IsPalettized || bitmap.Width < 2 || bitmap.Height < 2)
    {
        return null;
    }

    var first = bitmap.PaletteIndices[0];
    for (var column = 0; column < bitmap.Width; column++)
    {
        if (bitmap.PaletteIndices[column] != first ||
            bitmap.PaletteIndices[((bitmap.Height - 1) * bitmap.Width) + column] != first)
        {
            return null;
        }
    }

    for (var row = 0; row < bitmap.Height; row++)
    {
        if (bitmap.PaletteIndices[row * bitmap.Width] != first ||
            bitmap.PaletteIndices[(row * bitmap.Width) + bitmap.Width - 1] != first)
        {
            return null;
        }
    }

    return first;
}

static int ContactSheets(Options options)
{
    var output = options.Output ?? "assets/staging/sheets";
    Directory.CreateDirectory(output);
    var written = 0;
    foreach (var path in options.Archives)
    {
        var reader = new PortableExecutableResourceReader(File.ReadAllBytes(path));
        var name = Path.GetFileNameWithoutExtension(path);
        var tiles = new List<ContactSheet.Tile>();
        foreach (var entry in reader.OfType(PortableExecutableResourceReader.BitmapResourceType))
        {
            try
            {
                tiles.Add(new ContactSheet.Tile(
                    entry.Name.ToString(),
                    DeviceIndependentBitmap.Decode(reader.GetData(entry).Span)));
            }
            catch (InvalidDataException)
            {
                // A sheet is a browsing aid; a resource we cannot decode is
                // reported by --report and simply absent here.
            }
        }

        foreach (var (sheet, index) in ContactSheet.Build(tiles).Select((sheet, index) => (sheet, index)))
        {
            var file = Path.Combine(output, $"{name}-{index:D3}.png");
            File.WriteAllBytes(file, sheet);
            written++;
        }
    }

    Console.Error.WriteLine($"wrote {written} sheets to {output}");
    return 0;
}

static int Extract(Options options)
{
    var manifest = ArtManifest.Load(options.ManifestPath!);
    var output = options.Output ?? "src/Imperialism.Client/art";
    var readers = new Dictionary<string, PortableExecutableResourceReader>(StringComparer.OrdinalIgnoreCase);
    var problems = new List<string>();
    var written = 0;

    foreach (var group in manifest.Entries.GroupBy(entry => entry.Archive, StringComparer.OrdinalIgnoreCase))
    {
        var archivePath = Path.Combine(options.DataDirectory, group.Key);
        if (!File.Exists(archivePath))
        {
            problems.Add($"missing archive {archivePath}");
            continue;
        }

        readers[group.Key] = new PortableExecutableResourceReader(File.ReadAllBytes(archivePath));
    }

    foreach (var entry in manifest.Entries)
    {
        if (!readers.TryGetValue(entry.Archive, out var reader))
        {
            continue;
        }

        var resource = reader
            .OfType(PortableExecutableResourceReader.BitmapResourceType)
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name.ToString(), entry.Resource, StringComparison.Ordinal));
        if (resource is null)
        {
            problems.Add($"{entry.Archive} has no bitmap named {entry.Resource}");
            continue;
        }

        var bitmap = DeviceIndependentBitmap.Decode(reader.GetData(resource).Span);
        var pixels = ArtManifest.Render(bitmap, entry);
        var target = Path.Combine(output, entry.Path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        var png = PngWriter.Encode(entry.Width(bitmap), entry.Height(bitmap), pixels);
        WriteIfChanged(target, png);
        written++;
    }

    foreach (var font in manifest.Fonts)
    {
        var source = Path.Combine(options.DataDirectory, font.Source);
        if (!File.Exists(source))
        {
            problems.Add($"missing font {source}");
            continue;
        }

        var target = Path.Combine(output, font.Path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        WriteIfChanged(target, File.ReadAllBytes(source));
        written++;
    }

    foreach (var problem in problems)
    {
        Console.Error.WriteLine(problem);
    }

    var total = manifest.Entries.Count + manifest.Fonts.Count;
    Console.Error.WriteLine($"extracted {written} of {total} manifest entries to {output}");
    return problems.Count == 0 ? 0 : 1;
}

static void WriteIfChanged(string path, byte[] content)
{
    // Rewriting an identical file would still update its timestamp and re-trigger
    // Godot's importer, so compare before writing.
    if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(content))
    {
        return;
    }

    File.WriteAllBytes(path, content);
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

internal enum RunMode
{
    Report,
    Probe,
    ContactSheet,
    Manifest,
}

internal sealed record Options(
    RunMode Mode,
    string[] Archives,
    string[] Probes,
    string? ManifestPath,
    string DataDirectory,
    string? Output)
{
    public static Options Parse(string[] args)
    {
        var mode = (RunMode?)null;
        var archives = new List<string>();
        var probes = new List<string>();
        string? manifestPath = null;
        var dataDirectory = ".";
        string? output = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--report":
                    mode = RunMode.Report;
                    break;
                case "--probe":
                    mode = RunMode.Probe;
                    probes.Add(ReadValue(args, ref index));
                    break;
                case "--contact-sheet":
                    mode = RunMode.ContactSheet;
                    break;
                case "--manifest":
                    mode = RunMode.Manifest;
                    manifestPath = ReadValue(args, ref index);
                    break;
                case "--archive":
                    archives.Add(ReadValue(args, ref index));
                    break;
                case "--data-dir":
                    dataDirectory = ReadValue(args, ref index);
                    break;
                case "--output":
                    output = ReadValue(args, ref index);
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'.");
            }
        }

        if (mode is null)
        {
            throw new ArgumentException(
                "One of --report, --probe <resource>, --contact-sheet, or --manifest <path> is required.");
        }

        if (mode != RunMode.Manifest && archives.Count == 0)
        {
            throw new ArgumentException("At least one --archive is required.");
        }

        return new Options(
            mode.Value,
            archives.ToArray(),
            probes.ToArray(),
            manifestPath,
            dataDirectory,
            output);
    }

    private static string ReadValue(string[] args, ref int index)
    {
        if (++index >= args.Length)
        {
            throw new ArgumentException($"Option '{args[index - 1]}' requires a value.");
        }

        return args[index];
    }
}
