namespace TestShieldAI.Api.Persistence;

public sealed class ProjectTestRunRecord
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public required string Decision { get; set; }

    public int PassedCount { get; set; }

    public int FailedCount { get; set; }

    public int ErrorCount { get; set; }

    public int FindingCount { get; set; }

    public required string ResultsJson { get; set; }

    public ProjectRecord Project { get; set; } = null!;
}
