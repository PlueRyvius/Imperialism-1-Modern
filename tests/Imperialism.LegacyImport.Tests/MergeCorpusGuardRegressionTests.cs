using System.Reflection;
using Xunit;

namespace Imperialism.LegacyImport.Tests;

public sealed class MergeCorpusGuardRegressionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void ConfiguredCorpusMustRejectIncompleteDirectory(int scenarioCount)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "merge-corpus-" + Guid.NewGuid());
        Directory.CreateDirectory(path);
        var previous = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        try
        {
            for (var i = 1; i <= scenarioCount; i++)
                foreach (var extension in new[] { ".map", ".scn", ".inf" })
                    File.WriteAllBytes(Path.Combine(path, "s" + i + extension), []);
            Environment.SetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR", path);
            var guard = typeof(CorpusFactAttribute).GetMethod("RequireScenarioDirectory", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(guard);
            // Both an empty directory and nine synthetic scenarios must fail visibly.
            Assert.ThrowsAny<Exception>(() => guard.Invoke(null, null));
        }
        finally
        {
            Environment.SetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR", previous);
            Directory.Delete(path, true);
        }
    }
}
