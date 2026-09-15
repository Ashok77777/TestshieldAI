namespace TestShieldAI.Api.Contracts;

public sealed class RiskSummaryDto
{
    public required string Level { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required IReadOnlyList<string> Reasons { get; init; }

    public required IReadOnlyList<string> Recommendations { get; init; }
}
