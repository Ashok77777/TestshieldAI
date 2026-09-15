namespace TestShieldAI.Api.Contracts;

public sealed class CoverageDto
{
    public required int TotalOperations { get; init; }

    public required int CoveredOperations { get; init; }

    public required int UncoveredOperations { get; init; }

    public required decimal CoveragePercent { get; init; }

    public required int AiCoveredOperations { get; init; }

    public required CoverageScenariosDto Scenarios { get; init; }

    public required IReadOnlyList<CoverageGapDto> Gaps { get; init; }
}

public sealed class CoverageScenariosDto
{
    public required int HappyPath { get; init; }

    public required int Negative { get; init; }

    public required int AiPositive { get; init; }

    public required int AiNegative { get; init; }

    public required int AiEdge { get; init; }
}

public sealed class CoverageGapDto
{
    public required string SpecKey { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string Reason { get; init; }
}
