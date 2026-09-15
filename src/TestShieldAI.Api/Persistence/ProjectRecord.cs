namespace TestShieldAI.Api.Persistence;

public sealed class ProjectRecord
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string BaseUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<OpenApiSpecificationRecord> Specifications { get; set; } = [];

    public ProjectBaselineRecord? Baseline { get; set; }

    public ProjectTestRunRecord? LastRun { get; set; }
}
