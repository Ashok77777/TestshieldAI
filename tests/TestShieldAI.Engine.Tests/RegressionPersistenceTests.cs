using Microsoft.EntityFrameworkCore;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class RegressionPersistenceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"testshield-persist-{Guid.NewGuid():N}.db");
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly AppDbContext _db;

    public RegressionPersistenceTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _db = new AppDbContext(_options);
        _db.Database.Migrate();
    }

    [Fact]
    public async Task Baseline_CreateAndRead_RoundTripsSnapshots()
    {
        var project = await CreateProjectAsync();
        var store = new ProjectBaselineStore(_db);
        var snapshots = new[]
        {
            Snapshot(
                GeneratedApiTestKind.HappyPath,
                "GET",
                "/pets",
                200,
                new ImportedSchema(
                    "object",
                    ["id", "name"],
                    new Dictionary<string, ImportedSchema>
                    {
                        ["id"] = new("integer", [], new Dictionary<string, ImportedSchema>(), [], null),
                        ["name"] = new("string", [], new Dictionary<string, ImportedSchema>(), ["active"], null)
                    },
                    [],
                    null),
                ContractValidationOutcome.Passed)
        };

        await store.SaveAsync(project.Id, snapshots, specificationId: Guid.NewGuid());

        var record = await store.FindByProjectIdAsync(project.Id);
        Assert.NotNull(record);
        Assert.Equal(project.Id, record.ProjectId);
        Assert.NotEqual(default, record.EstablishedAt);
        Assert.NotNull(record.SpecificationId);

        var loaded = await store.GetSnapshotsAsync(project.Id);
        var snapshot = Assert.Single(loaded);
        Assert.Equal(GeneratedApiTestKind.HappyPath, snapshot.Kind);
        Assert.Equal("GET", snapshot.SourceMethod);
        Assert.Equal("/pets", snapshot.SourcePath);
        Assert.Equal(200, snapshot.ExpectedStatus);
        Assert.Equal(ContractValidationOutcome.Passed, snapshot.Outcome);
        Assert.NotNull(snapshot.ExpectedResponseSchema);
        Assert.Equal("object", snapshot.ExpectedResponseSchema.Type);
        Assert.Equal(["id", "name"], snapshot.ExpectedResponseSchema.Required);
        Assert.Equal("integer", snapshot.ExpectedResponseSchema.Properties["id"].Type);
        Assert.Equal(["active"], snapshot.ExpectedResponseSchema.Properties["name"].Enum);
        Assert.DoesNotContain("body", record.SnapshotJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("baseUrl", record.SnapshotJson, StringComparison.OrdinalIgnoreCase);
        Assert.Null(snapshot.SpecKey);
    }

    [Fact]
    public async Task Baseline_SpecKey_RoundTripsThroughExistingSnapshotJson()
    {
        var project = await CreateProjectAsync();
        var store = new ProjectBaselineStore(_db);
        await store.SaveAsync(
            project.Id,
            [
                new RegressionTestSnapshot(
                    GeneratedApiTestKind.HappyPath,
                    "GET",
                    "/pets",
                    "GET",
                    "/pets",
                    200,
                    null,
                    ContractValidationOutcome.Passed,
                    specKey: "Customer")
            ]);

        var snapshot = Assert.Single(await store.GetSnapshotsAsync(project.Id));
        var record = await store.FindByProjectIdAsync(project.Id);
        Assert.Equal("Customer", snapshot.SpecKey);
        Assert.Contains("specKey", record!.SnapshotJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openapi", record.SnapshotJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paths", record.SnapshotJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Baseline_Gate1SnapshotsWithoutSpecKey_DeserializeAsEmptyAndMatch()
    {
        const string json = """
            [{"kind":"HappyPath","sourceMethod":"GET","sourcePath":"/pets","method":"GET","pathTemplate":"/pets","expectedStatus":200,"outcome":"Passed"}]
            """;

        var snapshot = Assert.Single(RegressionPersistenceJson.DeserializeSnapshots(json));
        Assert.Null(snapshot.SpecKey);

        var current = new RegressionTestSnapshot(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets",
            "GET",
            "/pets",
            200,
            null,
            ContractValidationOutcome.Passed);

        var result = new RegressionDetector().Detect([snapshot], [current]);
        Assert.Equal(RegressionDecision.Safe, result.Decision);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task Baseline_Replace_SameProjectId_KeepsSingleRow()
    {
        var project = await CreateProjectAsync();
        var store = new ProjectBaselineStore(_db);
        await store.SaveAsync(project.Id, [Snapshot("GET", "/pets", 200)]);
        var original = await store.FindByProjectIdAsync(project.Id);
        Assert.NotNull(original);

        await store.SaveAsync(project.Id, [Snapshot("POST", "/orders", 201)]);

        var replaced = await store.FindByProjectIdAsync(project.Id);
        Assert.NotNull(replaced);
        Assert.Equal(original.Id, replaced.Id);
        Assert.Equal(1, await _db.ProjectBaselines.CountAsync(record => record.ProjectId == project.Id));
        var snapshot = Assert.Single(await store.GetSnapshotsAsync(project.Id));
        Assert.Equal("POST", snapshot.SourceMethod);
        Assert.Equal("/orders", snapshot.SourcePath);
        Assert.Equal(201, snapshot.ExpectedStatus);
    }

    [Fact]
    public async Task LastRun_CreateAndRead_RoundTripsCompactResults()
    {
        var project = await CreateProjectAsync();
        var store = new ProjectTestRunStore(_db);
        var save = CompactRun("Safe", passedCount: 1, failedCount: 0, "/pets", "Passed");

        await store.SaveAsync(project.Id, save);

        var record = await store.FindByProjectIdAsync(project.Id);
        Assert.NotNull(record);
        Assert.Equal(project.Id, record.ProjectId);
        Assert.Equal("Safe", record.Decision);
        Assert.Equal(1, record.PassedCount);
        Assert.Equal(0, record.FailedCount);
        Assert.Equal(0, record.ErrorCount);
        Assert.Equal(0, record.FindingCount);
        Assert.DoesNotContain("headers", record.ResultsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("duration", record.ResultsJson, StringComparison.OrdinalIgnoreCase);

        var results = await store.GetResultsAsync(project.Id);
        Assert.NotNull(results);
        var test = Assert.Single(results.Tests);
        Assert.Equal("GET", test.SourceMethod);
        Assert.Equal("/pets", test.SourcePath);
        Assert.Equal("Passed", test.Outcome);
        Assert.Empty(results.Findings);
    }

    [Fact]
    public async Task LastRun_Replace_SameProjectId_KeepsSingleRow()
    {
        var project = await CreateProjectAsync();
        var store = new ProjectTestRunStore(_db);
        await store.SaveAsync(project.Id, CompactRun("Safe", 1, 0, "/pets", "Passed"));
        var original = await store.FindByProjectIdAsync(project.Id);
        Assert.NotNull(original);

        await store.SaveAsync(
            project.Id,
            CompactRun("Block", passedCount: 0, failedCount: 1, "/orders", "Failed", findingCount: 1));

        var replaced = await store.FindByProjectIdAsync(project.Id);
        Assert.NotNull(replaced);
        Assert.Equal(original.Id, replaced.Id);
        Assert.Equal("Block", replaced.Decision);
        Assert.Equal(0, replaced.PassedCount);
        Assert.Equal(1, replaced.FailedCount);
        Assert.Equal(1, replaced.FindingCount);
        Assert.Equal(1, await _db.ProjectTestRuns.CountAsync(record => record.ProjectId == project.Id));
        var results = await store.GetResultsAsync(project.Id);
        Assert.NotNull(results);
        Assert.Equal("/orders", Assert.Single(results.Tests).SourcePath);
    }

    [Fact]
    public async Task Baseline_UniqueProjectId_IsEnforced()
    {
        var project = await CreateProjectAsync();
        await new ProjectBaselineStore(_db).SaveAsync(project.Id, [Snapshot("GET", "/pets", 200)]);

        await using var other = new AppDbContext(_options);
        other.ProjectBaselines.Add(Baseline(project.Id, "[]"));
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => other.SaveChangesAsync());
    }

    [Fact]
    public async Task LastRun_UniqueProjectId_IsEnforced()
    {
        var project = await CreateProjectAsync();
        await new ProjectTestRunStore(_db).SaveAsync(project.Id, CompactRun("Safe", 1, 0, "/pets", "Passed"));

        await using var other = new AppDbContext(_options);
        other.ProjectTestRuns.Add(LastRun(project.Id));
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => other.SaveChangesAsync());
    }

    [Fact]
    public async Task ProjectDeletion_CascadesBaselineAndLastRun()
    {
        var project = await CreateProjectAsync();
        await new ProjectBaselineStore(_db).SaveAsync(project.Id, [Snapshot("GET", "/pets", 200)]);
        await new ProjectTestRunStore(_db).SaveAsync(project.Id, CompactRun("Safe", 1, 0, "/pets", "Passed"));

        _db.Projects.Remove(await _db.Projects.SingleAsync(entity => entity.Id == project.Id));
        await _db.SaveChangesAsync();

        Assert.Empty(await _db.ProjectBaselines.ToListAsync());
        Assert.Empty(await _db.ProjectTestRuns.ToListAsync());
        Assert.Empty(await _db.Projects.ToListAsync());
    }

    public void Dispose()
    {
        _db.Dispose();
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
    }

    private async Task<ProjectRecord> CreateProjectAsync()
    {
        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(),
            Name = "Pets API",
            BaseUrl = "https://pets.example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    private static ProjectBaselineRecord Baseline(Guid projectId, string json) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            EstablishedAt = DateTimeOffset.UtcNow,
            SnapshotJson = json
        };

    private static ProjectTestRunRecord LastRun(Guid projectId) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CompletedAt = DateTimeOffset.UtcNow,
            Decision = "Safe",
            PassedCount = 0,
            FailedCount = 0,
            ErrorCount = 0,
            FindingCount = 0,
            ResultsJson = "{\"tests\":[],\"findings\":[]}"
        };

    private static ProjectTestRunSave CompactRun(
        string decision,
        int passedCount,
        int failedCount,
        string path,
        string outcome,
        int findingCount = 0) =>
        new()
        {
            Decision = decision,
            PassedCount = passedCount,
            FailedCount = failedCount,
            ErrorCount = 0,
            FindingCount = findingCount,
            Results = new PersistedTestRunResults
            {
                Tests =
                [
                    new PersistedTestRunCase
                    {
                        Kind = nameof(GeneratedApiTestKind.HappyPath),
                        SourceMethod = path == "/orders" ? "POST" : "GET",
                        SourcePath = path,
                        ExpectedStatus = 200,
                        ActualStatus = 200,
                        Outcome = outcome,
                        Failures = []
                    }
                ],
                Findings = findingCount == 0
                    ? []
                    :
                    [
                        new PersistedRegressionFinding
                        {
                            Category = nameof(RegressionFindingCategory.Regression),
                            Code = RegressionDetector.ValidationRegressed,
                            Severity = nameof(RegressionFindingSeverity.High),
                            Kind = nameof(GeneratedApiTestKind.HappyPath),
                            Method = "POST",
                            Path = path,
                            Expected = "Passed",
                            Current = "Failed",
                            JsonPath = null,
                            Message = "contract validation failed"
                        }
                    ]
            }
        };

    private static RegressionTestSnapshot Snapshot(string method, string path, int status) =>
        Snapshot(GeneratedApiTestKind.HappyPath, method, path, status, null, ContractValidationOutcome.Passed);

    private static RegressionTestSnapshot Snapshot(
        GeneratedApiTestKind kind,
        string method,
        string path,
        int status,
        ImportedSchema? schema,
        ContractValidationOutcome outcome) =>
        new(kind, method, path, method, path, status, schema, outcome);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
