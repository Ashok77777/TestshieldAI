using TestShieldAI.Engine;

namespace TestShieldAI.Api.Contracts;

public static class CoverageMapper
{
    public static CoverageDto ToDto(CoverageCalculationResult coverage) =>
        new()
        {
            TotalOperations = coverage.TotalOperations,
            CoveredOperations = coverage.CoveredOperations,
            UncoveredOperations = coverage.UncoveredOperations,
            CoveragePercent = coverage.CoveragePercent,
            AiCoveredOperations = coverage.AiCoveredOperations,
            Scenarios = new CoverageScenariosDto
            {
                HappyPath = coverage.Scenarios.HappyPath,
                Negative = coverage.Scenarios.Negative,
                AiPositive = coverage.Scenarios.AiPositive,
                AiNegative = coverage.Scenarios.AiNegative,
                AiEdge = coverage.Scenarios.AiEdge
            },
            Gaps = coverage.Gaps.Select(ToDto).ToList()
        };

    private static CoverageGapDto ToDto(CoverageGap gap) =>
        new()
        {
            SpecKey = gap.SpecKey,
            Method = gap.Method,
            Path = gap.Path,
            Reason = gap.Reason
        };
}
