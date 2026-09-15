namespace TestShieldAI.Engine;

public sealed class CoverageCalculationResult
{
    public CoverageCalculationResult(
        int totalOperations,
        int coveredOperations,
        int uncoveredOperations,
        decimal coveragePercent,
        int aiCoveredOperations,
        CoverageScenarioCounts scenarios,
        IReadOnlyList<CoverageGap> gaps)
    {
        TotalOperations = totalOperations;
        CoveredOperations = coveredOperations;
        UncoveredOperations = uncoveredOperations;
        CoveragePercent = coveragePercent;
        AiCoveredOperations = aiCoveredOperations;
        Scenarios = scenarios;
        Gaps = gaps ?? [];
    }

    public int TotalOperations { get; }

    public int CoveredOperations { get; }

    public int UncoveredOperations { get; }

    public decimal CoveragePercent { get; }

    public int AiCoveredOperations { get; }

    public CoverageScenarioCounts Scenarios { get; }

    public IReadOnlyList<CoverageGap> Gaps { get; }
}
