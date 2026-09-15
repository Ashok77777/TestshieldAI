using Microsoft.EntityFrameworkCore;

namespace TestShieldAI.Api.Persistence;

public sealed class OpenApiSpecificationStore : IOpenApiSpecificationStore
{
    private readonly AppDbContext _dbContext;

    public OpenApiSpecificationStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveRawSpecificationAsync(
        Guid projectId,
        string rawSpecification,
        CancellationToken cancellationToken = default)
    {
        var record = new OpenApiSpecificationRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RawSpecification = rawSpecification,
            ImportedAt = DateTimeOffset.UtcNow
        };

        _dbContext.OpenApiSpecifications.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetLatestRawSpecificationAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.OpenApiSpecifications
            .AsNoTracking()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        return records
            .OrderByDescending(record => record.ImportedAt)
            .Select(record => record.RawSpecification)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<OpenApiSpecificationRecord>> ListByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.OpenApiSpecifications
            .AsNoTracking()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(cancellationToken);
    }
}
