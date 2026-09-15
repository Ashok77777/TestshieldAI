namespace TestShieldAI.Engine;

public interface IRiskSummaryBuilder
{
    RiskSummaryResult Build(
        RegressionDetectionResult detection,
        CoverageCalculationResult? coverage = null,
        bool testsExecuted = true);
}
