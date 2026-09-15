namespace TestShieldAI.Api.Persistence;

public sealed class OpenApiSpecificationRecord
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public required string RawSpecification { get; set; }

    public DateTimeOffset ImportedAt { get; set; }

    public ProjectRecord Project { get; set; } = null!;
}
