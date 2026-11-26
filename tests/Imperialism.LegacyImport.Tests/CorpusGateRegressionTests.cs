using Xunit;
using Xunit.Sdk;

namespace Imperialism.LegacyImport.Tests;

[CollectionDefinition("Corpus guard environment", DisableParallelization = true)]
public sealed class CorpusGuardEnvironmentCollection { }

[Collection("Corpus guard environment")]
public sealed class CorpusGateRegressionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingConfigurationSkipsVisibly(string? configured)
    {
        WithEnvironment(configured, () =>
        {
            Assert.Contains("IMPERIALISM_SCENARIO_DIR", new CorpusFactAttribute().Skip);
            Assert.Throws<InvalidOperationException>(() => CorpusFactAttribute.RequireScenarioDirectory());
        });
    }

    [Fact]
    public void ConfiguredMissingDirectoryFailsInsteadOfSkipping()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", Guid.NewGuid().ToString("N"));
        WithEnvironment(directory, () =>
        {
            Assert.Null(new CorpusFactAttribute().Skip);
            var error = Assert.ThrowsAny<XunitException>(() => CorpusFactAttribute.RequireScenarioDirectory());
            Assert.Contains("points to missing directory", error.Message);
        });
    }

    [Theory]
    [InlineData(0, 0, 0, ".map")]
    [InlineData(9, 10, 10, ".map")]
    [InlineData(10, 9, 10, ".scn")]
    [InlineData(10, 10, 9, ".inf")]
    public void EachNumberedFileFamilyIsRequired(int maps, int scenarios, int infos, string missing)
    {
        WithFiles(maps, scenarios, infos, directory =>
        {
            Assert.Null(new CorpusFactAttribute().Skip);
            var error = Assert.ThrowsAny<XunitException>(() => CorpusFactAttribute.RequireScenarioDirectory());
            Assert.Contains($"Expected at least 10 numbered {missing} files", error.Message);
        });
    }

    [Fact]
    public void TenNumberedFilesPerFamilyPassTheCompletenessCheck()
    {
        WithFiles(10, 10, 10, directory => Assert.Equal(directory, CorpusFactAttribute.RequireScenarioDirectory()));
    }

    [Fact]
    public void UnnumberedFilesDoNotSubstituteForNumberedScenarios()
    {
        WithFiles(0, 10, 10, directory =>
        {
            for (var index = 1; index <= 10; index++)
                File.WriteAllBytes(Path.Combine(directory, $"unrelated-{index}.map"), []);
            Assert.ThrowsAny<XunitException>(() => CorpusFactAttribute.RequireScenarioDirectory());
        });
    }

    private static void WithFiles(int maps, int scenarios, int infos, Action<string> check)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "corpus-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var (extension, count) in new[] { (".map", maps), (".scn", scenarios), (".inf", infos) })
                for (var index = 1; index <= count; index++)
                    File.WriteAllBytes(Path.Combine(directory, $"s{index}{extension}"), []);
            WithEnvironment(directory, () => check(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void WithEnvironment(string? value, Action check)
    {
        var previous = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        try
        {
            Environment.SetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR", value);
            check();
        }
        finally { Environment.SetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR", previous); }
    }
}
