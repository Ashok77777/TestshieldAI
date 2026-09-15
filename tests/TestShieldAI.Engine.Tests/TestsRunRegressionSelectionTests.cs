using TestShieldAI.Api.TestsRun;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class TestsRunRegressionSelectionTests
{
    [Fact]
    public void SelectComparable_AiUnavailable_ExcludesAiFromBaselineAndCurrent()
    {
        var baseline = new[]
        {
            Snapshot(GeneratedApiTestKind.HappyPath, outcome: ContractValidationOutcome.Passed),
            Snapshot(GeneratedApiTestKind.AiPositive, outcome: ContractValidationOutcome.Passed, scenarioKey: "ai-1")
        };
        var current = new[]
        {
            Snapshot(GeneratedApiTestKind.HappyPath, outcome: ContractValidationOutcome.Passed)
        };

        var (selectedBaseline, selectedCurrent) = TestsRunRegressionSelection.SelectComparable(
            baseline,
            current,
            aiGenerationAvailable: false);

        Assert.Equal(GeneratedApiTestKind.HappyPath, Assert.Single(selectedBaseline).Kind);
        Assert.Equal(GeneratedApiTestKind.HappyPath, Assert.Single(selectedCurrent).Kind);
    }

    [Fact]
    public void SelectComparable_AiAvailable_IncludesAiOnlyWhenScenarioKeyMatchesBoth()
    {
        var baseline = new[]
        {
            Snapshot(GeneratedApiTestKind.HappyPath, outcome: ContractValidationOutcome.Passed),
            Snapshot(GeneratedApiTestKind.AiPositive, outcome: ContractValidationOutcome.Passed, scenarioKey: "keep"),
            Snapshot(GeneratedApiTestKind.AiNegative, outcome: ContractValidationOutcome.Passed, scenarioKey: "gone")
        };
        var current = new[]
        {
            Snapshot(GeneratedApiTestKind.HappyPath, outcome: ContractValidationOutcome.Passed),
            Snapshot(GeneratedApiTestKind.AiPositive, outcome: ContractValidationOutcome.Failed, scenarioKey: "keep"),
            Snapshot(GeneratedApiTestKind.AiEdge, outcome: ContractValidationOutcome.Failed, scenarioKey: "new")
        };

        var (selectedBaseline, selectedCurrent) = TestsRunRegressionSelection.SelectComparable(
            baseline,
            current,
            aiGenerationAvailable: true);

        Assert.Equal(2, selectedBaseline.Count);
        Assert.Equal(2, selectedCurrent.Count);
        Assert.Contains(selectedBaseline, snapshot => snapshot.ScenarioKey == "keep");
        Assert.Contains(selectedCurrent, snapshot => snapshot.ScenarioKey == "keep");
        Assert.DoesNotContain(selectedBaseline, snapshot => snapshot.ScenarioKey == "gone");
        Assert.DoesNotContain(selectedCurrent, snapshot => snapshot.ScenarioKey == "new");
    }

    [Fact]
    public void SelectComparable_SameScenarioKey_DifferentSpecKeys_RemainDistinct()
    {
        var baseline = new[]
        {
            Snapshot(GeneratedApiTestKind.AiPositive, ContractValidationOutcome.Passed, "keep", "Customer"),
            Snapshot(GeneratedApiTestKind.AiPositive, ContractValidationOutcome.Passed, "keep", "Order")
        };
        var current = new[]
        {
            Snapshot(GeneratedApiTestKind.AiPositive, ContractValidationOutcome.Failed, "keep", "Customer")
        };

        var (selectedBaseline, selectedCurrent) = TestsRunRegressionSelection.SelectComparable(
            baseline,
            current,
            aiGenerationAvailable: true);

        Assert.Equal("Customer", Assert.Single(selectedBaseline).SpecKey);
        Assert.Equal("Customer", Assert.Single(selectedCurrent).SpecKey);
    }

    [Fact]
    public void ShouldPromote_IgnoresAiFailures()
    {
        var current = new[]
        {
            Snapshot(GeneratedApiTestKind.HappyPath, outcome: ContractValidationOutcome.Passed),
            Snapshot(GeneratedApiTestKind.AiPositive, outcome: ContractValidationOutcome.Failed, scenarioKey: "ai-1")
        };

        Assert.True(TestsRunRegressionSelection.ShouldPromote(current));
        var promoted = TestsRunRegressionSelection.SnapshotsToPromote(current);
        Assert.Equal(GeneratedApiTestKind.HappyPath, Assert.Single(promoted).Kind);
    }

    [Fact]
    public void ShouldPromote_FalseWhenDeterministicFails()
    {
        var current = new[]
        {
            Snapshot(GeneratedApiTestKind.HappyPath, outcome: ContractValidationOutcome.Failed),
            Snapshot(GeneratedApiTestKind.AiPositive, outcome: ContractValidationOutcome.Passed, scenarioKey: "ai-1")
        };

        Assert.False(TestsRunRegressionSelection.ShouldPromote(current));
    }

    private static RegressionTestSnapshot Snapshot(
        GeneratedApiTestKind kind,
        ContractValidationOutcome outcome,
        string? scenarioKey = null,
        string? specKey = null) =>
        new(
            kind,
            "GET",
            "/pets",
            "GET",
            "/pets",
            200,
            expectedResponseSchema: null,
            outcome,
            scenarioKey,
            specKey);
}
