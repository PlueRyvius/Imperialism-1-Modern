using Xunit;

namespace Imperialism.Formats.Tests;

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

        return directory;
    }
}
