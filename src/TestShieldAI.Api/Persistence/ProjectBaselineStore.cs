using Microsoft.EntityFrameworkCore;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Persistence;

public sealed class ProjectBaselineStore : IProjectBaselineStore
{
    private readonly AppDbContext _dbContext;

    public ProjectBaselineStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProjectBaselineRecord?> FindByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectBaselines
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.ProjectId == projectId, cancellationToken);
    }

    public async Task<IReadOnlyList<RegressionTestSnapshot>> GetSnapshotsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var record = await FindByProjectIdAsync(projectId, cancellationToken);
        return record is null
            ? []
            : RegressionPersistenceJson.DeserializeSnapshots(record.SnapshotJson);
    }

    public async Task SaveAsync(
        Guid projectId,
        IReadOnlyList<RegressionTestSnapshot> snapshots,
        Guid? specificationId = null,
        CancellationToken cancellationToken = default)
    {
        var json = RegressionPersistenceJson.SerializeSnapshots(snapshots);
        var existing = await _dbContext.ProjectBaselines
            .FirstOrDefaultAsync(record => record.ProjectId == projectId, cancellationToken);

        if (existing is null)
        {
            _dbContext.ProjectBaselines.Add(new ProjectBaselineRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                EstablishedAt = DateTimeOffset.UtcNow,
                SpecificationId = specificationId,
                SnapshotJson = json
            });
        }
        else
        {
            existing.EstablishedAt = DateTimeOffset.UtcNow;
            existing.SpecificationId = specificationId;
            existing.SnapshotJson = json;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
