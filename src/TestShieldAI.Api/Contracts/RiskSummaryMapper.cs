using TestShieldAI.Engine;

namespace TestShieldAI.Api.Contracts;

public static class RiskSummaryMapper
{
    public static RiskSummaryDto ToDto(RiskSummaryResult risk) =>
        new()
        {
            Level = risk.Level.ToString(),
            Title = risk.Title,
            Summary = risk.Summary,
            Reasons = risk.Reasons,
            Recommendations = risk.Recommendations
        };
}
