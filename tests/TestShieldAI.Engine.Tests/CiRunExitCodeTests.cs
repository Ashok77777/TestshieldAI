using TestShieldAI.Ci;

namespace TestShieldAI.Engine.Tests;

public class CiRunExitCodeTests
{
    [Theory]
    [InlineData("Safe", 0)]
    [InlineData("safe", 0)]
    [InlineData("Review", 0)]
    [InlineData("REVIEW", 0)]
    [InlineData("Block", 1)]
    [InlineData("block", 1)]
    [InlineData("", 1)]
    [InlineData(null, 1)]
    [InlineData("Unknown", 1)]
    public void ForDecision_MapsSafeAndReviewToZeroAndBlockToOne(string? decision, int expected)
    {
        Assert.Equal(expected, CiRunExitCode.ForDecision(decision));
    }

    [Theory]
    [InlineData(200, "Safe", 0)]
    [InlineData(200, "Review", 0)]
    [InlineData(200, "Block", 1)]
    [InlineData(400, "Safe", 1)]
    [InlineData(500, "Review", 1)]
    [InlineData(404, "Block", 1)]
    public void ForRun_HttpFailureAlwaysFailsThePipeline(int status, string decision, int expected)
    {
        Assert.Equal(expected, CiRunExitCode.ForRun(status, decision));
    }

    [Fact]
    public void Review_DoesNotFailThePipeline()
    {
        Assert.Equal(CiRunExitCode.Success, CiRunExitCode.ForRun(200, "Review"));
    }

    [Fact]
    public void Block_FailsThePipeline()
    {
        Assert.Equal(CiRunExitCode.Failure, CiRunExitCode.ForRun(200, "Block"));
    }
}
