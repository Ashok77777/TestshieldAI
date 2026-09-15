namespace TestShieldAI.Api.Contracts;

public sealed class CreateProjectRequest
{
    public string? Name { get; set; }

    public string? BaseUrl { get; set; }
}

public sealed class ProjectResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string BaseUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
