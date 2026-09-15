using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class RiskSummaryBuilderTests
{
    private readonly IRiskSummaryBuilder _builder = new RiskSummaryBuilder();

    [Fact]
    public void Build_Safe_IsLow()
    {
        var result = _builder.Build(Detection(RegressionDecision.Safe));

        Assert.Equal(RiskSummaryLevel.Low, result.Level);
        Assert.Equal(RiskSummaryBuilder.SafeTitle, result.Title);
        Assert.Equal(RiskSummaryBuilder.SafeSummary, result.Summary);
        Assert.Empty(result.Recommendations);
        Assert.Equal(RegressionDecision.Safe, Detection(RegressionDecision.Safe).Decision);
    }

    [Fact]
    public void Build_ReviewWithOnlyMediumFinding_IsMedium()
    {
        var result = _builder.Build(Detection(
            RegressionDecision.Review,
            Finding(RegressionDetector.SchemaRequiredAdded, RegressionFindingSeverity.Medium)));

        Assert.Equal(RiskSummaryLevel.Medium, result.Level);
        Assert.Equal(RiskSummaryBuilder.ReviewTitle, result.Title);
        Assert.Equal(RiskSummaryBuilder.ReviewSummary, result.Summary);
        Assert.Equal(RiskSummaryBuilder.ReviewRecommendations, result.Recommendations);
    }

    [Fact]
    public void Build_ReviewWithHighFinding_IsMedium()
    {
        var result = _builder.Build(Detection(
            RegressionDecision.Review,
            Finding(RegressionDetector.SchemaTypeChanged, RegressionFindingSeverity.High)));

        Assert.Equal(RiskSummaryLevel.Medium, result.Level);
        Assert.Equal(RiskSummaryBuilder.ReviewTitle, result.Title);
        Assert.Equal(RegressionDecision.Review, Detection(RegressionDecision.Review).Decision);
    }

    [Fact]
    public void Build_Block_IsHigh()
    {
        var result = _builder.Build(Detection(
            RegressionDecision.Block,
            Finding(RegressionDetector.ValidationRegressed, RegressionFindingSeverity.High)));

        Assert.Equal(RiskSummaryLevel.High, result.Level);
        Assert.Equal(RiskSummaryBuilder.BlockTitle, result.Title);
        Assert.Equal(RiskSummaryBuilder.BlockSummary, result.Summary);
        Assert.Equal(RiskSummaryBuilder.BlockRecommendations, result.Recommendations);
    }

    [Fact]
    public void Build_SafeWithNoFindings_HasHumanReadableReason()
    {
        var result = _builder.Build(Detection(RegressionDecision.Safe));

        Assert.Equal(RiskSummaryBuilder.SafeNoFindingsReason, Assert.Single(result.Reasons));
    }

    [Fact]
    public void Build_ReviewWithNoFindings_HasBaselineReason()
    {
        var result = _builder.Build(Detection(RegressionDecision.Review));

        Assert.Equal(RiskSummaryLevel.Medium, result.Level);
        Assert.Equal(RiskSummaryBuilder.ReviewNoBaselineReason, Assert.Single(result.Reasons));
        Assert.Equal(RiskSummaryBuilder.ReviewRecommendations, result.Recommendations);
    }

    [Fact]
    public void Build_ContractDrift_CreatesUsefulReason()
    {
        var result = _builder.Build(Detection(
            RegressionDecision.Review,
            Finding(RegressionDetector.SchemaTypeChanged, RegressionFindingSeverity.High)));

        Assert.Equal("Response schema changed for GET /customers/{id}.", Assert.Single(result.Reasons));
    }

    [Fact]
    public void Build_ValidationRegression_CreatesUsefulReason()
    {
        var result = _builder.Build(Detection(
            RegressionDecision.Block,
            Finding(RegressionDetector.ValidationRegressed, RegressionFindingSeverity.High)));

        Assert.Equal(
            "Previously passing validation now fails for GET /customers/{id}.",
            Assert.Single(result.Reasons));
    }

    [Fact]
    public void Build_ExecutionRegression_CreatesUsefulReason()
    {
        var result = _builder.Build(Detection(
            RegressionDecision.Block,
            Finding(RegressionDetector.ExecutionRegressed, RegressionFindingSeverity.High)));

        Assert.Equal(
            "A previously passing test could not complete successfully for GET /customers/{id}.",
            Assert.Single(result.Reasons));
    }

    [Fact]
    public void Build_OperationRemovedAndTestRemoved_CreateUsefulReasons()
    {
        var result = _builder.Build(Detection(
            RegressionDecision.Review,
            Finding(RegressionDetector.OperationRemoved, RegressionFindingSeverity.High),
            Finding(RegressionDetector.TestRemoved, RegressionFindingSeverity.High, path: "/pets")));

        Assert.Equal(
            [
                "An operation was removed from the current API contract: GET /customers/{id}.",
                "A previously baselined test is no longer present for GET /pets."
            ],
            result.Reasons);
    }

    [Fact]
    public void Build_DuplicateFindings_DoNotCreateDuplicateReasons()
    {
        var duplicate = Finding(RegressionDetector.SchemaTypeChanged, RegressionFindingSeverity.High);
        var result = _builder.Build(Detection(RegressionDecision.Review, duplicate, duplicate));

        Assert.Equal("Response schema changed for GET /customers/{id}.", Assert.Single(result.Reasons));
    }

    [Fact]
    public void Build_CoverageBelow100_AddsCoverageContext()
    {
        var result = _builder.Build(
            Detection(RegressionDecision.Safe),
            Coverage(10, 8, 80m, 2));

        Assert.Equal(RiskSummaryLevel.Low, result.Level);
        Assert.Equal(2, result.Reasons.Count);
        Assert.Equal(RiskSummaryBuilder.SafeNoFindingsReason, result.Reasons[0]);
        Assert.Equal("API test coverage is 80%; 2 operations do not have deterministic tests.", result.Reasons[1]);
    }

    [Fact]
    public void Build_Coverage100_DoesNotAddCoverageWarning()
    {
        var result = _builder.Build(
            Detection(RegressionDecision.Safe),
            Coverage(2, 2, 100m, 0));

        Assert.Equal(RiskSummaryBuilder.SafeNoFindingsReason, Assert.Single(result.Reasons));
        Assert.DoesNotContain(result.Reasons, reason => reason.Contains("coverage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_Coverage_DoesNotChangeDecisionLevelForSafe()
    {
        var result = _builder.Build(
            Detection(RegressionDecision.Safe),
            Coverage(10, 0, 0m, 10));

        Assert.Equal(RiskSummaryLevel.Low, result.Level);
        Assert.Equal(RiskSummaryBuilder.SafeTitle, result.Title);
    }

    [Fact]
    public void Build_NullCoverage_DoesNotInventCoverageText()
    {
        var result = _builder.Build(Detection(RegressionDecision.Safe), coverage: null);

        Assert.DoesNotContain(result.Reasons, reason => reason.Contains("coverage", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Reasons, reason => reason.Contains("%", StringComparison.Ordinal));
        Assert.Equal(RiskSummaryBuilder.SafeNoFindingsReason, Assert.Single(result.Reasons));
    }

    [Fact]
    public void Build_PreservesFindingOrderWhenReasonsDiffer()
    {
        var result = _builder.Build(Detection(
            RegressionDecision.Review,
            Finding(RegressionDetector.SchemaRequiredAdded, RegressionFindingSeverity.Medium, path: "/pets"),
            Finding(RegressionDetector.SchemaTypeChanged, RegressionFindingSeverity.High, path: "/orders")));

        Assert.Equal(
            [
                "Response schema changed for GET /pets.",
                "Response schema changed for GET /orders."
            ],
            result.Reasons);
        Assert.Equal(RiskSummaryLevel.Medium, result.Level);
        Assert.Equal(RiskSummaryBuilder.ReviewRecommendations, result.Recommendations);
    }

    private static RegressionDetectionResult Detection(
        RegressionDecision decision,
        params RegressionFinding[] findings) =>
        new(findings, decision);

    private static RegressionFinding Finding(
        string code,
        RegressionFindingSeverity severity,
        string method = "GET",
        string path = "/customers/{id}") =>
        new(
            CategoryFor(code),
            code,
            severity,
            GeneratedApiTestKind.HappyPath,
            method,
            path,
            "expected",
            "current",
            jsonPath: null,
            "message");

    private static RegressionFindingCategory CategoryFor(string code) =>
        code switch
        {
            RegressionDetector.ValidationRegressed or RegressionDetector.ExecutionRegressed =>
                RegressionFindingCategory.Regression,
            RegressionDetector.ExecutionError => RegressionFindingCategory.ExecutionFailure,
            _ => RegressionFindingCategory.ContractDrift
        };

    private static CoverageCalculationResult Coverage(
        int total,
        int covered,
        decimal percent,
        int uncovered) =>
        new(
            total,
            covered,
            uncovered,
            percent,
            aiCoveredOperations: 0,
            new CoverageScenarioCounts(0, 0, 0, 0, 0),
            []);
}
