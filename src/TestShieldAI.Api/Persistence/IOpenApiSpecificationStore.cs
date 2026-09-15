namespace TestShieldAI.Api.Persistence;

public interface IOpenApiSpecificationStore
{
    Task SaveRawSpecificationAsync(Guid projectId, string rawSpecification, CancellationToken cancellationToken = default);

    Task<string?> GetLatestRawSpecificationAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpenApiSpecificationRecord>> ListByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
