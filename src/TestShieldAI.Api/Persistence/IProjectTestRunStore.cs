namespace TestShieldAI.Api.Persistence;

public interface IProjectTestRunStore
{
    Task<ProjectTestRunRecord?> FindByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<PersistedTestRunResults?> GetResultsAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task SaveAsync(Guid projectId, ProjectTestRunSave save, CancellationToken cancellationToken = default);
}
