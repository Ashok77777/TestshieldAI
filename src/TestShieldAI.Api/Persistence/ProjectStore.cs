using Microsoft.EntityFrameworkCore;

namespace TestShieldAI.Api.Persistence;

public sealed class ProjectStore : IProjectStore
{
    private readonly AppDbContext _dbContext;

    public ProjectStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectRecord> CreateAsync(string name, string baseUrl, CancellationToken cancellationToken = default)
    {
        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(),
            Name = name,
            BaseUrl = baseUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<IReadOnlyList<ProjectRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<ProjectRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Projects.AsNoTracking().FirstOrDefaultAsync(project => project.Id == id, cancellationToken);
    }
}
