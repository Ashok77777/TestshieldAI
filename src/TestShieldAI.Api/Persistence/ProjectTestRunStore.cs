using Microsoft.EntityFrameworkCore;

namespace TestShieldAI.Api.Persistence;

public sealed class ProjectTestRunStore : IProjectTestRunStore
{
    private readonly AppDbContext _dbContext;

    public ProjectTestRunStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProjectTestRunRecord?> FindByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectTestRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.ProjectId == projectId, cancellationToken);
    }

    public async Task<PersistedTestRunResults?> GetResultsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var record = await FindByProjectIdAsync(projectId, cancellationToken);
        return record is null ? null : RegressionPersistenceJson.DeserializeResults(record.ResultsJson);
    }

    public async Task SaveAsync(
        Guid projectId,
        ProjectTestRunSave save,
        CancellationToken cancellationToken = default)
    {
        var json = RegressionPersistenceJson.SerializeResults(save.Results);
        var existing = await _dbContext.ProjectTestRuns
            .FirstOrDefaultAsync(record => record.ProjectId == projectId, cancellationToken);

        if (existing is null)
        {
            _dbContext.ProjectTestRuns.Add(new ProjectTestRunRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                CompletedAt = DateTimeOffset.UtcNow,
                Decision = save.Decision,
                PassedCount = save.PassedCount,
                FailedCount = save.FailedCount,
                ErrorCount = save.ErrorCount,
                FindingCount = save.FindingCount,
                ResultsJson = json
            });
        }
        else
        {
            existing.CompletedAt = DateTimeOffset.UtcNow;
            existing.Decision = save.Decision;
            existing.PassedCount = save.PassedCount;
            existing.FailedCount = save.FailedCount;
            existing.ErrorCount = save.ErrorCount;
            existing.FindingCount = save.FindingCount;
            existing.ResultsJson = json;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
