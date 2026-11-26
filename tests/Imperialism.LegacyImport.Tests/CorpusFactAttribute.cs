using Xunit;

namespace Imperialism.LegacyImport.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class CorpusFactAttribute : FactAttribute
{
    public CorpusFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR")))
        {
            Skip = "Set IMPERIALISM_SCENARIO_DIR to the complete legal local scenario corpus to run this test.";
        }
    }

    public static string RequireScenarioDirectory()
    {
        var directory = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("CorpusFact should have skipped this test because IMPERIALISM_SCENARIO_DIR is unset.");
        }

        Assert.True(Directory.Exists(directory), $"IMPERIALISM_SCENARIO_DIR points to missing directory: {directory}.");
        AssertAtLeastTenNumberedFiles(directory, ".map");
        AssertAtLeastTenNumberedFiles(directory, ".scn");
        AssertAtLeastTenNumberedFiles(directory, ".inf");

        return directory;
    }

    private static void AssertAtLeastTenNumberedFiles(string directory, string extension)
    {
        var count = Directory.GetFiles(directory, $"*{extension}")
            .Count(IsNumberedScenarioFile);
        Assert.True(
            count >= 10,
            $"Expected at least 10 numbered {extension} files in the configured scenario corpus, found {count} in {directory}.");
    }

    private static bool IsNumberedScenarioFile(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        return stem.Length > 1 && stem[0] == 's' && int.TryParse(stem.AsSpan(1), out _);
    }
}
