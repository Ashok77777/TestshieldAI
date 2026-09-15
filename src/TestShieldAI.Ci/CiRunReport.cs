namespace TestShieldAI.Ci;

public sealed class CiRunReport
{
    public static CiRunReport Empty { get; } = new();

    public string Decision { get; init; } = "";

    public string RiskLevel { get; init; } = "";

    public string RiskTitle { get; init; } = "";

    public string RiskSummary { get; init; } = "";

    public IReadOnlyList<string> RiskReasons { get; init; } = [];

    public decimal? CoveragePercent { get; init; }

    public int FindingCount { get; init; }

    public IReadOnlyList<CiRunFinding> Findings { get; init; } = [];

    public int PassedCount { get; init; }

    public int FailedCount { get; init; }

    public int ErrorCount { get; init; }
}

public sealed class CiRunFinding
{
    public string Category { get; init; } = "";

    public string Message { get; init; } = "";
}
