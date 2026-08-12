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
}
