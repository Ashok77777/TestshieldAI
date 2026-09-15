namespace TestShieldAI.Engine;

public sealed class RiskSummaryResult
{
    public RiskSummaryResult(
        RiskSummaryLevel level,
        string title,
        string summary,
        IReadOnlyList<string> reasons,
        IReadOnlyList<string>? recommendations = null)
    {
        Level = level;
        Title = title;
        Summary = summary;
        Reasons = reasons ?? [];
        Recommendations = recommendations ?? [];
    }

    public RiskSummaryLevel Level { get; }

    public string Title { get; }

    public string Summary { get; }

    public IReadOnlyList<string> Reasons { get; }

    public IReadOnlyList<string> Recommendations { get; }
}
