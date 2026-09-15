namespace TestShieldAI.Api.Persistence;

public sealed class ProjectBaselineRecord
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public DateTimeOffset EstablishedAt { get; set; }

    public Guid? SpecificationId { get; set; }

    public required string SnapshotJson { get; set; }

    public ProjectRecord Project { get; set; } = null!;
}
