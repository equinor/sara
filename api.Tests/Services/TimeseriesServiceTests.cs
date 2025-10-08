using api.Services;
using Xunit;

namespace Api.Test.Services;

public class TimeseriesServiceTests
{
    [Theory]
    // Exact integers -> unchanged
    [InlineData(290.0, 290)]
    [InlineData(100.0, 100)]
    [InlineData(23.0, 23)]
    // Typical positives -> normal floor
    [InlineData(290.34, 290)]
    [InlineData(23.9, 23)]
    [InlineData(289.4, 289)]
    [InlineData(290.6, 290)]
    // Floating-point “just below” the integer-> snap up to the integer
    [InlineData(289.9999999, 290)]
    [InlineData(289.999, 290)]
    // Upper side near next integer should NOT bump (we want floor semantics)
    [InlineData(290.9, 290)]
    [InlineData(290.05, 290)]
    [InlineData(289.95, 290)]
    // Some negatives for sanity
    [InlineData(-0.1, -1)]
    [InlineData(-0.001, 0)]
    [InlineData(-1.1, -2)]
    public static void FloorWithTolerance_Parametrized(double input, int expected)
    {
        var actual = TimeseriesService.FloorWithTolerance(input);
        Assert.Equal(expected, actual);
    }
}
