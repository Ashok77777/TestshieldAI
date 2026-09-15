using System.Globalization;

namespace TestShieldAI.Engine;

public sealed class RiskSummaryBuilder : IRiskSummaryBuilder
{
    public const string SafeTitle = "Low risk";
    public const string ReviewTitle = "Review recommended";
    public const string BlockTitle = "Release blocked";

    public const string SafeSummary =
        "All current tests passed and no regression or contract drift was detected.";

    public const string ReviewSummary =
        "The current run requires human review before release.";

    public const string BlockSummary =
        "A previously passing test has regressed or a high-severity contract change was detected.";

    public const string SafeNoFindingsReason =
        "All current deterministic tests passed and no regression or contract drift was detected.";

    public const string ReviewNoBaselineReason =
        "Baseline is not yet established; review the current test results before treating this run as trusted.";

    public const string GenerateNoExecutionReason =
        "Tests were generated; run the suite to evaluate regression risk against the baseline.";

    public const string ReviewRecommendationResults = "Review the current test results.";
    public const string ReviewRecommendationBaseline = "Confirm the baseline is trusted before release.";
    public const string ReviewRecommendationDrift = "Review contract drift findings if present.";

    public const string BlockRecommendationFix = "Fix the failing regression before release.";
    public const string BlockRecommendationReview = "Review the affected API operation and contract.";
    public const string BlockRecommendationRerun = "Run the suite again after the fix.";

    public static readonly IReadOnlyList<string> ReviewRecommendations =
    [
        ReviewRecommendationResults,
        ReviewRecommendationBaseline,
        ReviewRecommendationDrift
    ];

    public static readonly IReadOnlyList<string> BlockRecommendations =
    [
        BlockRecommendationFix,
        BlockRecommendationReview,
        BlockRecommendationRerun
    ];

    public RiskSummaryResult Build(
        RegressionDetectionResult detection,
        CoverageCalculationResult? coverage = null,
        bool testsExecuted = true)
    {
        detection ??= new RegressionDetectionResult([], RegressionDecision.Safe);
        var findings = detection.Findings ?? [];
        var level = LevelFor(detection.Decision);
        var reasons = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var finding in findings)
        {
            TryAdd(reasons, seen, ReasonFor(finding));
        }

        if (reasons.Count == 0)
        {
            if (!testsExecuted)
            {
                TryAdd(reasons, seen, GenerateNoExecutionReason);
            }
            else if (detection.Decision is RegressionDecision.Review)
            {
                TryAdd(reasons, seen, ReviewNoBaselineReason);
            }
            else if (detection.Decision is RegressionDecision.Safe)
            {
                TryAdd(reasons, seen, SafeNoFindingsReason);
            }
        }

        AddCoverageReason(reasons, seen, coverage);

        return new RiskSummaryResult(
            level,
            TitleFor(detection.Decision),
            SummaryFor(detection.Decision),
            reasons,
            RecommendationsFor(detection.Decision));
    }

    private static RiskSummaryLevel LevelFor(RegressionDecision decision) =>
        decision switch
        {
            RegressionDecision.Block => RiskSummaryLevel.High,
            RegressionDecision.Review => RiskSummaryLevel.Medium,
            _ => RiskSummaryLevel.Low
        };

    private static string TitleFor(RegressionDecision decision) =>
        decision switch
        {
            RegressionDecision.Block => BlockTitle,
            RegressionDecision.Review => ReviewTitle,
            _ => SafeTitle
        };

    private static string SummaryFor(RegressionDecision decision) =>
        decision switch
        {
            RegressionDecision.Block => BlockSummary,
            RegressionDecision.Review => ReviewSummary,
            _ => SafeSummary
        };

    private static IReadOnlyList<string> RecommendationsFor(RegressionDecision decision) =>
        decision switch
        {
            RegressionDecision.Block => BlockRecommendations,
            RegressionDecision.Review => ReviewRecommendations,
            _ => []
        };

    private static string ReasonFor(RegressionFinding finding)
    {
        var operation = $"{finding.Method} {finding.Path}";
        return finding.Code switch
        {
            RegressionDetector.OperationRemoved =>
                $"An operation was removed from the current API contract: {operation}.",
            RegressionDetector.TestRemoved =>
                $"A previously baselined test is no longer present for {operation}.",
            RegressionDetector.ValidationRegressed =>
                $"Previously passing validation now fails for {operation}.",
            RegressionDetector.ExecutionRegressed =>
                $"A previously passing test could not complete successfully for {operation}.",
            RegressionDetector.ExecutionError =>
                $"A test could not complete successfully for {operation}.",
            RegressionDetector.ExpectedStatusChanged =>
                $"Expected status changed for {operation}.",
            _ when finding.Category is RegressionFindingCategory.ContractDrift =>
                $"Response schema changed for {operation}.",
            _ when finding.Category is RegressionFindingCategory.Regression =>
                $"Previously passing validation now fails for {operation}.",
            _ => $"A finding was detected for {operation}."
        };
    }

    private static void AddCoverageReason(
        List<string> reasons,
        HashSet<string> seen,
        CoverageCalculationResult? coverage)
    {
        if (coverage is null || coverage.CoveragePercent >= 100m)
        {
            return;
        }

        var percent = FormatPercent(coverage.CoveragePercent);
        var uncovered = coverage.UncoveredOperations;
        var operations = uncovered == 1
            ? "1 operation does not have a deterministic test"
            : $"{uncovered.ToString(CultureInfo.InvariantCulture)} operations do not have deterministic tests";
        TryAdd(reasons, seen, $"API test coverage is {percent}%; {operations}.");
    }

    private static string FormatPercent(decimal percent) =>
        percent == decimal.Truncate(percent)
            ? decimal.Truncate(percent).ToString(CultureInfo.InvariantCulture)
            : percent.ToString("0.##", CultureInfo.InvariantCulture);

    private static void TryAdd(List<string> reasons, HashSet<string> seen, string reason)
    {
        if (seen.Add(reason))
        {
            reasons.Add(reason);
        }
    }
}
