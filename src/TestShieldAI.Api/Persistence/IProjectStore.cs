namespace TestShieldAI.Api.Persistence;

public interface IProjectStore
{
    Task<ProjectRecord> CreateAsync(string name, string baseUrl, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<ProjectRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
