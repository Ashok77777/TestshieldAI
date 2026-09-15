using TestShieldAI.Engine;

namespace TestShieldAI.Api.Persistence;

public interface IProjectBaselineStore
{
    Task<ProjectBaselineRecord?> FindByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegressionTestSnapshot>> GetSnapshotsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Guid projectId,
        IReadOnlyList<RegressionTestSnapshot> snapshots,
        Guid? specificationId = null,
        CancellationToken cancellationToken = default);
}
