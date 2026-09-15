namespace TestShieldAI.Api.Contracts;

public sealed class AiRunSummaryDto
{
    public required bool Enabled { get; init; }

    public required int GeneratedCount { get; init; }

    public required int AcceptedCount { get; init; }

    public required int WarningCount { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}
