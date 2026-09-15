namespace TestShieldAI.Engine;

public sealed class RegressionDetectionResult
{
    public RegressionDetectionResult(
        IReadOnlyList<RegressionFinding> findings,
        RegressionDecision decision)
    {
        Findings = findings;
        Decision = decision;
    }

    public IReadOnlyList<RegressionFinding> Findings { get; }

    public RegressionDecision Decision { get; }
}
