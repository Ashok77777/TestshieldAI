using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TestShieldAI.Api.Ai;
using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Endpoints;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class TestsRunAiEndpointTests :
    IClassFixture<TestsRunWebApplicationFactory>,
    IClassFixture<TestsRunAiWebApplicationFactory>
{
    private const string GetPetsSpec = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": { "type": "array" }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private const string PostPetsSpec = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets": {
              "post": {
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "required": ["name"],
                        "properties": {
                          "name": { "type": "string" }
                        }
                      }
                    }
                  }
                },
                "responses": {
                  "201": {
                    "description": "created",
                    "content": {
                      "application/json": {
                        "schema": { "type": "object" }
                      }
                    }
                  },
                  "400": {
                    "description": "bad request",
                    "content": {
                      "application/json": {
                        "schema": { "type": "object" }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly TestsRunWebApplicationFactory _disabledFactory;
    private readonly TestsRunAiWebApplicationFactory _aiFactory;
    private readonly HttpClient _disabledClient;
    private readonly HttpClient _aiClient;

    public TestsRunAiEndpointTests(
        TestsRunWebApplicationFactory disabledFactory,
        TestsRunAiWebApplicationFactory aiFactory)
    {
        _disabledFactory = disabledFactory;
        _aiFactory = aiFactory;
        _disabledClient = disabledFactory.CreateClient();
        _aiClient = aiFactory.CreateClient();
        disabledFactory.Runner.Reset();
        aiFactory.Reset();
    }

    [Fact]
    public async Task Run_AiDisabled_RunsDeterministicTestsOnlyWithoutCompletionCalls()
    {
        var project = await CreateProjectAsync(_disabledClient, "https://pets.example.com");
        await ImportAsync(_disabledClient, project.Id, GetPetsSpec);
        _disabledFactory.Runner.ResultFactory = test => Succeed(test, "[]");

        var response = await _disabledClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(body.Ai.Enabled);
        Assert.Equal(0, body.Ai.GeneratedCount);
        Assert.Equal(0, body.Ai.AcceptedCount);
        var result = Assert.Single(body.Results);
        Assert.Equal(nameof(GeneratedApiTestKind.HappyPath), result.Kind);
        Assert.Equal(nameof(RegressionDecision.Safe), body.Decision);
        using var scope = _disabledFactory.Services.CreateScope();
        Assert.IsType<NoOpAiScenarioGenerator>(scope.ServiceProvider.GetRequiredService<IAiScenarioGenerator>());
        Assert.IsType<NoOpAiCompletionClient>(scope.ServiceProvider.GetRequiredService<IAiCompletionClient>());
        Assert.Equal(1, body.Coverage.TotalOperations);
        Assert.Equal(1, body.Coverage.CoveredOperations);
        Assert.Equal(100m, body.Coverage.CoveragePercent);
        Assert.Equal(0, body.Coverage.AiCoveredOperations);
        Assert.Equal(1, body.Coverage.Scenarios.HappyPath);
        Assert.Equal(0, body.Coverage.Scenarios.AiPositive);
        Assert.Empty(body.Coverage.Gaps);
    }

    [Fact]
    public async Task Run_AiEnabled_AddsNormalizedPositiveNegativeAndEdgeThroughSameRunner()
    {
        var project = await CreateProjectAsync(_aiClient, "https://pets.example.com");
        await ImportAsync(_aiClient, project.Id, PostPetsSpec);
        _aiFactory.Completion.Completion = PostScenariosJson();
        _aiFactory.Runner.ResultFactory = test => Succeed(test, "{}");

        var response = await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body.Ai.Enabled);
        Assert.Equal(3, body.Ai.GeneratedCount);
        Assert.Equal(3, body.Ai.AcceptedCount);
        Assert.Equal(0, body.Ai.WarningCount);
        Assert.Equal(5, body.Results.Count);
        Assert.Contains(body.Results, result => result.Kind == nameof(GeneratedApiTestKind.HappyPath));
        Assert.Contains(body.Results, result => result.Kind == nameof(GeneratedApiTestKind.NegativeMissingRequiredBody));
        Assert.Contains(body.Results, result => result.Kind == nameof(GeneratedApiTestKind.AiPositive));
        Assert.Contains(body.Results, result => result.Kind == nameof(GeneratedApiTestKind.AiNegative));
        Assert.Contains(body.Results, result => result.Kind == nameof(GeneratedApiTestKind.AiEdge));
        Assert.Equal(5, _aiFactory.Runner.LastTests!.Count);
        Assert.Contains(_aiFactory.Runner.LastTests, test => test.Kind == GeneratedApiTestKind.AiPositive);
        Assert.True(_aiFactory.Completion.Calls > 0);
        Assert.All(body.Results, result =>
            Assert.Equal(nameof(ContractValidationOutcome.Passed), result.Validation.Outcome));
        Assert.Equal(nameof(RegressionDecision.Safe), body.Decision);
        Assert.DoesNotContain("sk-", JsonSerializer.Serialize(body), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", JsonSerializer.Serialize(body), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, body.Coverage.TotalOperations);
        Assert.Equal(1, body.Coverage.CoveredOperations);
        Assert.Equal(100m, body.Coverage.CoveragePercent);
        Assert.Equal(1, body.Coverage.AiCoveredOperations);
        Assert.Equal(1, body.Coverage.Scenarios.HappyPath);
        Assert.Equal(1, body.Coverage.Scenarios.Negative);
        Assert.Equal(1, body.Coverage.Scenarios.AiPositive);
        Assert.Equal(1, body.Coverage.Scenarios.AiNegative);
        Assert.Equal(1, body.Coverage.Scenarios.AiEdge);
        Assert.Empty(body.Coverage.Gaps);
        Assert.Equal(nameof(RiskSummaryLevel.Low), body.Risk.Level);
        Assert.Equal(RiskSummaryBuilder.SafeTitle, body.Risk.Title);
        Assert.Empty(body.Risk.Recommendations);
        Assert.Equal(nameof(RegressionDecision.Safe), body.Decision);
        Assert.True(body.Ai.Enabled);
        Assert.Equal(3, body.Ai.AcceptedCount);
    }

    [Fact]
    public async Task Run_AiEnabled_ReconcilesExpectedStatusBeforeExecution()
    {
        var project = await CreateProjectAsync(_aiClient, "https://pets.example.com");
        await ImportAsync(_aiClient, project.Id, GetPetsSpec);
        _aiFactory.Completion.Completion = """
            [
              {
                "type": "Negative",
                "method": "GET",
                "pathTemplate": "/pets",
                "path": "/pets",
                "parameters": [],
                "expectedStatus": 999,
                "rationale": "Hallucinated status."
              }
            ]
            """;
        _aiFactory.Runner.ResultFactory = test => Succeed(test, "[]");

        var response = await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        var ai = Assert.Single(body.Results, result => result.Kind == nameof(GeneratedApiTestKind.AiNegative));
        Assert.Equal(400, ai.ExpectedStatus);
        Assert.Equal(400, Assert.Single(_aiFactory.Runner.LastTests!, test => test.Kind == GeneratedApiTestKind.AiNegative).ExpectedStatus);
        Assert.Equal(1, body.Ai.AcceptedCount);
    }

    [Fact]
    public async Task Run_InvalidAiScenario_IsDroppedWithWarning()
    {
        var project = await CreateProjectAsync(_aiClient, "https://pets.example.com");
        await ImportAsync(_aiClient, project.Id, GetPetsSpec);
        _aiFactory.Completion.Completion = """
            [
              {
                "type": "Positive",
                "method": "GET",
                "pathTemplate": "/missing",
                "path": "/missing",
                "expectedStatus": 200,
                "rationale": "Unknown endpoint."
              },
              {
                "type": "Negative",
                "method": "GET",
                "pathTemplate": "/pets",
                "path": "/pets",
                "expectedStatus": 400,
                "rationale": "Valid extra."
              }
            ]
            """;
        _aiFactory.Runner.ResultFactory = test => Succeed(test, "[]");

        var response = await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(2, body.Ai.GeneratedCount);
        Assert.Equal(1, body.Ai.AcceptedCount);
        Assert.True(body.Ai.WarningCount > 0);
        Assert.Contains(body.Ai.Warnings, warning => warning.Contains("Unknown endpoint", StringComparison.Ordinal));
        Assert.Equal(2, body.Results.Count);
        Assert.DoesNotContain(body.Results, result => result.Path == "/missing");
    }

    [Fact]
    public async Task Run_AiProviderFailure_Returns200KeepsDeterministicTestsAndWarns()
    {
        var project = await CreateProjectAsync(_aiClient, "https://pets.example.com");
        await ImportAsync(_aiClient, project.Id, GetPetsSpec);
        _aiFactory.Completion.Exception = new InvalidOperationException("provider unavailable");
        _aiFactory.Runner.ResultFactory = test => Succeed(test, "[]");

        var response = await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(nameof(GeneratedApiTestKind.HappyPath), Assert.Single(body.Results).Kind);
        Assert.Equal(0, body.Ai.AcceptedCount);
        Assert.True(body.Ai.WarningCount > 0);
        Assert.Contains(body.Ai.Warnings, warning => warning.Contains("provider unavailable", StringComparison.Ordinal));
        Assert.Equal(nameof(RegressionDecision.Safe), body.Decision);
        Assert.DoesNotContain("sk-", string.Join('\n', body.Ai.Warnings), StringComparison.Ordinal);
        Assert.Equal(1, body.Coverage.TotalOperations);
        Assert.Equal(1, body.Coverage.CoveredOperations);
        Assert.Equal(100m, body.Coverage.CoveragePercent);
        Assert.Equal(0, body.Coverage.AiCoveredOperations);
        Assert.Equal(1, body.Coverage.Scenarios.HappyPath);
        Assert.Equal(0, body.Coverage.Scenarios.AiPositive);
        Assert.Empty(body.Coverage.Gaps);
    }

    [Fact]
    public async Task Run_MatchingAiScenarioKey_ParticipatesInRegression()
    {
        var project = await CreateProjectAsync(_aiClient, "https://pets.example.com");
        await ImportAsync(_aiClient, project.Id, GetPetsSpec);
        _aiFactory.Completion.Completion = ValidGetScenarioJson();
        _aiFactory.Runner.ResultFactory = test => Succeed(test, "[]");
        await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        _aiFactory.Runner.ResultFactory = test =>
            test.Kind == GeneratedApiTestKind.AiNegative
                ? FailStatus(test, 500)
                : Succeed(test, "[]");
        var response = await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(nameof(RegressionDecision.Block), body.Decision);
        var finding = Assert.Single(body.Findings);
        Assert.Equal(RegressionDetector.ValidationRegressed, finding.Code);
        Assert.Equal(nameof(GeneratedApiTestKind.AiNegative), finding.Kind);
        Assert.Equal(nameof(ContractValidationOutcome.Passed),
            Assert.Single(body.Results, result => result.Kind == nameof(GeneratedApiTestKind.HappyPath)).Validation.Outcome);
        Assert.NotNull(body.BaselineEstablishedAt);
        var snapshots = await GetBaselineSnapshotsAsync(_aiFactory, project.Id);
        Assert.DoesNotContain(snapshots, snapshot => snapshot.Kind == GeneratedApiTestKind.AiNegative);
        Assert.Contains(snapshots, snapshot => snapshot.Kind == GeneratedApiTestKind.HappyPath);
    }

    [Fact]
    public async Task Run_AiUnavailable_DoesNotCreateTestRemovedFindings()
    {
        var project = await CreateProjectAsync(_aiClient, "https://pets.example.com");
        await ImportAsync(_aiClient, project.Id, GetPetsSpec);
        _aiFactory.Completion.Completion = ValidGetScenarioJson();
        _aiFactory.Runner.ResultFactory = test => Succeed(test, "[]");
        await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        Assert.Contains(
            await GetBaselineSnapshotsAsync(_aiFactory, project.Id),
            snapshot => snapshot.Kind == GeneratedApiTestKind.AiNegative);

        _aiFactory.Completion.Exception = new InvalidOperationException("timeout");
        var response = await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(nameof(GeneratedApiTestKind.HappyPath), Assert.Single(body.Results).Kind);
        Assert.DoesNotContain(body.Findings, finding => finding.Code == RegressionDetector.TestRemoved);
        Assert.Equal(nameof(RegressionDecision.Safe), body.Decision);
        Assert.True(body.Ai.WarningCount > 0);
    }

    [Fact]
    public async Task Run_DeterministicFailure_ProtectsExistingBaseline()
    {
        var project = await CreateProjectAsync(_aiClient, "https://pets.example.com");
        await ImportAsync(_aiClient, project.Id, GetPetsSpec);
        _aiFactory.Completion.Completion = ValidGetScenarioJson();
        _aiFactory.Runner.ResultFactory = test => Succeed(test, "[]");
        await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var original = await FindBaselineAsync(_aiFactory, project.Id);
        Assert.NotNull(original);

        _aiFactory.Runner.ResultFactory = test =>
            test.Kind == GeneratedApiTestKind.HappyPath
                ? Succeed(test, "{}")
                : Succeed(test, "[]");
        var response = await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(nameof(RegressionDecision.Block), body.Decision);
        Assert.Equal(original.Id, (await FindBaselineAsync(_aiFactory, project.Id))!.Id);
        Assert.Equal(original.SnapshotJson, (await FindBaselineAsync(_aiFactory, project.Id))!.SnapshotJson);
    }

    [Fact]
    public async Task Run_AiFailure_DoesNotPreventDeterministicBaselinePromotion()
    {
        var project = await CreateProjectAsync(_aiClient, "https://pets.example.com");
        await ImportAsync(_aiClient, project.Id, GetPetsSpec);
        _aiFactory.Completion.Completion = ValidGetScenarioJson();
        _aiFactory.Runner.ResultFactory = test => Succeed(test, "[]");
        await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var original = await FindBaselineAsync(_aiFactory, project.Id);
        Assert.NotNull(original);

        _aiFactory.Completion.Exception = new InvalidOperationException("provider down");
        var response = await _aiClient.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(nameof(RegressionDecision.Safe), body.Decision);
        var replaced = await FindBaselineAsync(_aiFactory, project.Id);
        Assert.NotNull(replaced);
        Assert.NotEqual(original.SnapshotJson, replaced.SnapshotJson);
        var snapshots = await GetBaselineSnapshotsAsync(_aiFactory, project.Id);
        Assert.Equal(GeneratedApiTestKind.HappyPath, Assert.Single(snapshots).Kind);
    }

    [Fact]
    public void Factory_DisabledSelectsNoOp_EnabledSelectsLlm()
    {
        var client = new NoOpAiCompletionClient();

        Assert.IsType<NoOpAiScenarioGenerator>(
            AiScenarioGeneratorFactory.Create(new AiOptions(), client));
        Assert.IsType<LlmAiScenarioGenerator>(
            AiScenarioGeneratorFactory.Create(
                new AiOptions { Enabled = true, Provider = AiProvider.OpenAI, MaxScenariosPerOperation = 4 },
                client));
    }

    private static string ValidGetScenarioJson() => """
        [
          {
            "type": "Negative",
            "method": "GET",
            "pathTemplate": "/pets",
            "path": "/pets",
            "parameters": [],
            "expectedStatus": 400,
            "rationale": "Missing pet should be rejected."
          }
        ]
        """;

    private static string PostScenariosJson() => """
        [
          {
            "type": "Positive",
            "method": "POST",
            "pathTemplate": "/pets",
            "path": "/pets",
            "requestBody": { "name": "Rex" },
            "expectedStatus": 201,
            "rationale": "Create a named pet."
          },
          {
            "type": "Negative",
            "method": "POST",
            "pathTemplate": "/pets",
            "path": "/pets",
            "requestBody": { "name": "" },
            "expectedStatus": 400,
            "rationale": "Empty name is invalid."
          },
          {
            "type": "Edge",
            "method": "POST",
            "pathTemplate": "/pets",
            "path": "/pets",
            "requestBody": { "name": "A very long pet name" },
            "expectedStatus": 201,
            "rationale": "Long name boundary."
          }
        ]
        """;

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient client, string baseUrl)
    {
        var response = await client.PostAsJsonAsync(
            ProjectsEndpoint.Route,
            new { name = "Pets API", baseUrl });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        return project;
    }

    private static async Task ImportAsync(HttpClient client, Guid projectId, string specification)
    {
        var json = JsonSerializer.Serialize(new { specification });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(SpecsImportEndpoint.RouteFor(projectId), content);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ProjectBaselineRecord?> FindBaselineAsync(
        TestsRunAiWebApplicationFactory factory,
        Guid projectId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProjectBaselineStore>()
            .FindByProjectIdAsync(projectId);
    }

    private static async Task<IReadOnlyList<RegressionTestSnapshot>> GetBaselineSnapshotsAsync(
        TestsRunAiWebApplicationFactory factory,
        Guid projectId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProjectBaselineStore>()
            .GetSnapshotsAsync(projectId);
    }

    private static ApiTestExecutionResult Succeed(GeneratedApiTestCase test, string body) =>
        new(
            test.Kind,
            test.Method,
            test.Path,
            test.SourceMethod,
            test.SourcePath,
            test.ExpectedStatus,
            test.ExpectedStatus,
            new Dictionary<string, string>(),
            body,
            durationMs: 1,
            statusMatched: true,
            error: null);

    private static ApiTestExecutionResult FailStatus(GeneratedApiTestCase test, int actualStatus) =>
        new(
            test.Kind,
            test.Method,
            test.Path,
            test.SourceMethod,
            test.SourcePath,
            test.ExpectedStatus,
            actualStatus,
            new Dictionary<string, string>(),
            body: "{}",
            durationMs: 1,
            statusMatched: false,
            error: null);
}

public sealed class FakeAiCompletionClient : IAiCompletionClient
{
    public int Calls { get; private set; }

    public string Completion { get; set; } = "[]";

    public Exception? Exception { get; set; }

    public void Reset()
    {
        Calls = 0;
        Completion = "[]";
        Exception = null;
    }

    public Task<string> CompleteJsonAsync(string prompt, CancellationToken cancellationToken = default)
    {
        _ = prompt;
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        if (Exception is not null)
        {
            throw Exception;
        }

        return Task.FromResult(Completion);
    }
}

public class TestsRunAiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"testshield-ai-run-{Guid.NewGuid():N}.db");

    public StubApiTestRunner Runner => (StubApiTestRunner)Services.GetRequiredService<IApiTestRunner>();

    public FakeAiCompletionClient Completion => Services.GetRequiredService<FakeAiCompletionClient>();

    public void Reset()
    {
        Runner.Reset();
        Completion.Reset();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TestShield", $"Data Source={_dbPath}");
        builder.UseSetting("Ai:Enabled", "true");
        builder.UseSetting("Ai:Provider", "OpenAI");
        builder.UseSetting("Ai:ModelOrDeployment", "gpt-4o-mini");
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IApiTestRunner)).ToList())
            {
                services.Remove(descriptor);
            }

            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IAiCompletionClient)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IApiTestRunner, StubApiTestRunner>();
            services.AddSingleton<FakeAiCompletionClient>();
            services.AddSingleton<IAiCompletionClient>(provider => provider.GetRequiredService<FakeAiCompletionClient>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
    }

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
