using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;

namespace Imperialism.Presentation.Tests;

public sealed class TransportTextTests
{
    [Theory]
    [InlineData(1, "Spring, 1882")]
    [InlineData(2, "Summer, 1882")]
    [InlineData(3, "Fall, 1882")]
    [InlineData(4, "Winter, 1882")]
    public void TransportUsesQuarterNamesWithoutChangingTheCoreDate(int quarter, string expected)
    {
        var date = new TurnDate(1882, quarter);
        Assert.Equal(expected, TransportText.FormatDate(date));
        Assert.Equal($"1882 Q{quarter}", date.ToString());
        Assert.Equal(new TurnDate(1882, quarter), date);
    }
}
