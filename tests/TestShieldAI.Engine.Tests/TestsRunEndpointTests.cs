using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Endpoints;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class TestsRunEndpointTests : IClassFixture<TestsRunWebApplicationFactory>
{
    private const string Spec = """
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

    private const string TwoOperationSpec = """
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
            },
            "/health": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
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

    private const string ObjectSpec = """
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

    private const string SpecWithServers = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Pets", "version": "1.0.0" },
          "servers": [
            { "url": "https://from-spec.example.com" }
          ],
          "paths": {
            "/pets": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
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

    private readonly TestsRunWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TestsRunEndpointTests(TestsRunWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.Runner.Reset();
    }

    [Fact]
    public async Task Run_AfterImport_Returns200WithResultsForProjectBaseUrl()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, Spec);

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("https://pets.example.com", body.BaseUrl);
        Assert.NotEmpty(body.Results);
        Assert.Contains(body.Results, result => result.Method == "GET" && result.Path == "/pets");

        var runner = (StubApiTestRunner)_factory.Services.GetRequiredService<IApiTestRunner>();
        Assert.Equal("https://pets.example.com", runner.LastBaseUrl);
        Assert.NotNull(runner.LastTests);
        Assert.Equal(runner.LastTests.Count, body.Results.Count);
        Assert.All(body.Results, result => Assert.NotNull(result.Validation));
        Assert.Equal("Pets", Assert.Single(body.Results).SpecKey);
        Assert.All(runner.LastTests!, test => Assert.Equal("Pets", test.SpecKey));
    }

    [Fact]
    public async Task Run_OpenApiServers_AreNotUsed_ProjectBaseUrlRemainsExecutionBaseUrl()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, SpecWithServers);

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<RunTestsResponse>(json, JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("https://pets.example.com", body.BaseUrl);
        Assert.Equal("https://pets.example.com", _factory.Runner.LastBaseUrl);
        Assert.DoesNotContain("https://from-spec.example.com", json, StringComparison.Ordinal);
        Assert.All(_factory.Runner.LastTests!, test => Assert.StartsWith("/", test.Path, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_LatestPerSpecKey_ExecutesMergedCurrentSetAgainstProjectBaseUrl()
    {
        var project = await CreateProjectAsync("https://gateway.example.com");
        await ImportAsync(project.Id, MiniOpenApiSpec("Pets", "1.0.0", "/legacy"));
        await Task.Delay(25);
        await ImportAsync(project.Id, MiniOpenApiSpec("Order", "1.0.0", "/orders"));
        await Task.Delay(25);
        await ImportAsync(project.Id, MiniOpenApiSpec("Pets", "2.0.0", "/pets"));
        _factory.Runner.ResultFactory = test => Succeed(test, "{}");

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("https://gateway.example.com", body.BaseUrl);
        Assert.Equal("https://gateway.example.com", _factory.Runner.LastBaseUrl);
        Assert.Equal(2, body.Results.Count);
        Assert.Contains(body.Results, result => result.SpecKey == "Pets" && result.SourcePath == "/pets");
        Assert.Contains(body.Results, result => result.SpecKey == "Order" && result.SourcePath == "/orders");
        Assert.DoesNotContain(body.Results, result => result.SourcePath == "/legacy");
        Assert.Equal(1, await CountBaselinesAsync(project.Id));
        Assert.Equal(2, body.Coverage.TotalOperations);
        Assert.Equal(2, body.Coverage.CoveredOperations);
        Assert.Equal(100m, body.Coverage.CoveragePercent);
        Assert.Empty(body.Coverage.Gaps);
    }

    [Fact]
    public async Task Run_NewUnrelatedSpecKey_DoesNotCreateTestRemoved()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, MiniOpenApiSpec("Customer", "1.0.0", "/pets"));
        _factory.Runner.ResultFactory = test => Succeed(test, "{}");
        await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        await Task.Delay(25);
        await ImportAsync(project.Id, MiniOpenApiSpec("Order", "1.0.0", "/orders"));
        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(2, body.Results.Count);
        Assert.DoesNotContain(body.Findings, finding => finding.Code == RegressionDetector.TestRemoved);
        Assert.DoesNotContain(body.Findings, finding => finding.Code == RegressionDetector.OperationRemoved);
        Assert.Equal(1, await CountBaselinesAsync(project.Id));
    }

    [Fact]
    public async Task Run_RemovedOperationInsideExistingSpecKey_IsDetected()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, TwoOperationSpec);
        _factory.Runner.ResultFactory = test => test.Path == "/pets"
            ? Succeed(test, "[]")
            : Succeed(test, "{}");
        await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        await Task.Delay(25);
        await ImportAsync(project.Id, Spec);
        _factory.Runner.ResultFactory = test => Succeed(test, "[]");
        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        var finding = Assert.Single(body.Findings);
        Assert.Equal(RegressionDetector.OperationRemoved, finding.Code);
        Assert.Equal("/health", finding.Path);
        Assert.DoesNotContain(body.Findings, item => item.Path == "/pets");
        Assert.Equal(1, await CountBaselinesAsync(project.Id));
    }

    [Fact]
    public async Task Run_RemovedEntireSpecKey_IsDetectedForThatSpecificationOnly()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, MiniOpenApiSpec("Customer", "1.0.0", "/pets"));
        await Task.Delay(25);
        await ImportAsync(project.Id, MiniOpenApiSpec("Order", "1.0.0", "/orders"));
        _factory.Runner.ResultFactory = test => Succeed(test, "{}");
        await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        await Task.Delay(25);
        await ImportAsync(project.Id, EmptyPathsDocument("Order"));
        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Contains(body.Results, result => result.SpecKey == "Customer" && result.SourcePath == "/pets");
        Assert.DoesNotContain(body.Results, result => result.SpecKey == "Order" && result.SourcePath == "/orders");
        var finding = Assert.Single(body.Findings);
        Assert.Equal(RegressionDetector.OperationRemoved, finding.Code);
        Assert.Equal("/orders", finding.Path);
        Assert.DoesNotContain(body.Findings, item => item.Path == "/pets");
    }

    [Fact]
    public async Task Run_SameMethodAndPath_DifferentSpecKeys_DoNotCollide()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, MiniOpenApiSpec("Customer", "1.0.0", "/pets"));
        await Task.Delay(25);
        await ImportAsync(project.Id, MiniOpenApiSpec("Order", "1.0.0", "/pets"));
        _factory.Runner.ResultFactory = test => Succeed(test, "{}");

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);

        Assert.NotNull(body);
        Assert.Equal(2, body.Results.Count);
        Assert.Equal(2, _factory.Runner.LastTests!.Count);
        Assert.Contains(body.Results, result => result.SpecKey == "Customer" && result.SourcePath == "/pets");
        Assert.Contains(body.Results, result => result.SpecKey == "Order" && result.SourcePath == "/pets");
        Assert.Empty(body.Findings);
        Assert.Equal(nameof(RegressionDecision.Safe), body.Decision);
        Assert.Equal(2, body.Coverage.TotalOperations);
        Assert.Equal(2, body.Coverage.CoveredOperations);
        Assert.Equal(100m, body.Coverage.CoveragePercent);
        Assert.Empty(body.Coverage.Gaps);
    }

    [Fact]
    public async Task Run_SuccessfulExecutionAndValidContract_ReturnsPassedValidation()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, Spec);
        _factory.Runner.ResultFactory = test => Succeed(test, "[]");

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        var result = Assert.Single(body.Results);
        Assert.Equal(200, result.ActualStatus);
        Assert.Null(result.Error);
        Assert.Equal(nameof(ContractValidationOutcome.Passed), result.Validation.Outcome);
        Assert.True(result.Validation.StatusValid);
        Assert.True(result.Validation.SchemaValid);
        Assert.Empty(result.Validation.Failures);
    }

    [Fact]
    public async Task Run_ContractValidationFailure_ReturnsFailedValidation()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, Spec);

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        var result = Assert.Single(body.Results);
        Assert.Equal(200, result.ActualStatus);
        Assert.Equal(nameof(ContractValidationOutcome.Failed), result.Validation.Outcome);
        Assert.True(result.Validation.StatusValid);
        Assert.False(result.Validation.SchemaValid);
        Assert.Equal(1, body.Coverage.TotalOperations);
        Assert.Equal(1, body.Coverage.CoveredOperations);
        Assert.Equal(100m, body.Coverage.CoveragePercent);
        Assert.Empty(body.Coverage.Gaps);
        Assert.Contains(result.Validation.Failures, failure => failure.Code == ApiContractValidator.SchemaType);
        Assert.All(result.Validation.Failures, failure =>
        {
            Assert.False(string.IsNullOrWhiteSpace(failure.Code));
            Assert.False(string.IsNullOrWhiteSpace(failure.JsonPath));
            Assert.False(string.IsNullOrWhiteSpace(failure.Message));
        });
    }

    [Fact]
    public async Task Run_ExecutionError_ReturnsErrorValidationWithoutFailingTheRequest()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, Spec);
        _factory.Runner.ResultFactory = test => Error(test, "The request timed out.");

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        var result = Assert.Single(body.Results);
        Assert.Equal("The request timed out.", result.Error);
        Assert.Null(result.ActualStatus);
        Assert.Equal(nameof(ContractValidationOutcome.Error), result.Validation.Outcome);
        Assert.False(result.Validation.StatusValid);
        Assert.Null(result.Validation.SchemaValid);
        var failure = Assert.Single(result.Validation.Failures);
        Assert.Equal(ApiContractValidator.ExecutionError, failure.Code);
        Assert.Equal("The request timed out.", failure.Message);
    }

    [Fact]
    public async Task Run_MultipleGeneratedTests_EachResultHasItsOwnValidation()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, TwoOperationSpec);
        _factory.Runner.ResultFactory = test => test.Path == "/pets"
            ? Succeed(test, "[]")
            : Succeed(test, """{"ok":true}""");

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(2, body.Results.Count);
        var pets = Assert.Single(body.Results, result => result.Path == "/pets");
        var health = Assert.Single(body.Results, result => result.Path == "/health");
        Assert.Equal("[]", pets.Body);
        Assert.Equal("""{"ok":true}""", health.Body);
        Assert.Equal(nameof(ContractValidationOutcome.Passed), pets.Validation.Outcome);
        Assert.Equal(nameof(ContractValidationOutcome.Passed), health.Validation.Outcome);
        Assert.NotSame(pets.Validation, health.Validation);
        Assert.All(body.Results, result =>
        {
            Assert.True(result.Validation.StatusValid);
            Assert.True(result.Validation.SchemaValid);
            Assert.Empty(result.Validation.Failures);
        });
    }

    [Fact]
    public async Task Run_FirstAllPass_CreatesBaselineAndReturnsSafe()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, Spec);
        _factory.Runner.ResultFactory = test => Succeed(test, "[]");

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(nameof(RegressionDecision.Safe), body.Decision);
        Assert.Empty(body.Findings);
        Assert.NotNull(body.BaselineEstablishedAt);
        Assert.Equal(nameof(ContractValidationOutcome.Passed), Assert.Single(body.Results).Validation.Outcome);

        var baseline = await FindBaselineAsync(project.Id);
        Assert.NotNull(baseline);
        Assert.Equal(body.BaselineEstablishedAt, baseline.EstablishedAt);
        Assert.Single(await GetBaselineSnapshotsAsync(project.Id));

        var lastRun = await FindLastRunAsync(project.Id);
        Assert.NotNull(lastRun);
        Assert.Equal(nameof(RegressionDecision.Safe), lastRun.Decision);
        Assert.Equal(1, lastRun.PassedCount);
        Assert.Equal(0, lastRun.FailedCount);
        Assert.Equal(0, lastRun.ErrorCount);
        Assert.Equal(0, lastRun.FindingCount);
        Assert.Equal(1, body.Coverage.TotalOperations);
        Assert.Equal(1, body.Coverage.CoveredOperations);
        Assert.Equal(100m, body.Coverage.CoveragePercent);
        Assert.Empty(body.Coverage.Gaps);
        Assert.DoesNotContain("coveragePercent", lastRun.ResultsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coveragePercent", baseline.SnapshotJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nameof(RiskSummaryLevel.Low), body.Risk.Level);
        Assert.Equal(RiskSummaryBuilder.SafeTitle, body.Risk.Title);
        Assert.Equal(RiskSummaryBuilder.SafeSummary, body.Risk.Summary);
        Assert.Empty(body.Risk.Recommendations);
        Assert.Contains(RiskSummaryBuilder.SafeNoFindingsReason, body.Risk.Reasons);
        Assert.DoesNotContain("risk", lastRun.ResultsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RiskSummaryBuilder.SafeTitle, lastRun.ResultsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_SecondUnchangedAllPass_ReturnsSafeAndReplacesBaseline()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, Spec);
        _factory.Runner.ResultFactory = test => Succeed(test, "[]");
        await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var original = await FindBaselineAsync(project.Id);
        Assert.NotNull(original);

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(nameof(RegressionDecision.Safe), body.Decision);
        Assert.Empty(body.Findings);
        Assert.NotNull(body.BaselineEstablishedAt);

        var replaced = await FindBaselineAsync(project.Id);
        Assert.NotNull(replaced);
        Assert.Equal(original.Id, replaced.Id);
        Assert.Equal(1, await CountBaselinesAsync(project.Id));
        Assert.Equal(1, await CountLastRunsAsync(project.Id));
    }

    [Fact]
    public async Task Run_CurrentValidationFailureWithoutBaseline_DoesNotCreateBaseline()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, Spec);

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(nameof(RegressionDecision.Review), body.Decision);
        Assert.Null(body.BaselineEstablishedAt);
        Assert.DoesNotContain(body.Findings, finding => finding.Code == RegressionDetector.ValidationRegressed);
        Assert.Equal(nameof(ContractValidationOutcome.Failed), Assert.Single(body.Results).Validation.Outcome);
        Assert.Null(await FindBaselineAsync(project.Id));
        var lastRun = await FindLastRunAsync(project.Id);
        Assert.NotNull(lastRun);
        Assert.Equal(1, lastRun.FailedCount);
        Assert.Equal(nameof(RegressionDecision.Review), lastRun.Decision);
        Assert.Equal(nameof(RiskSummaryLevel.Medium), body.Risk.Level);
        Assert.Equal(RiskSummaryBuilder.ReviewTitle, body.Risk.Title);
        Assert.Equal(RiskSummaryBuilder.ReviewSummary, body.Risk.Summary);
        Assert.Equal(RiskSummaryBuilder.ReviewRecommendations, body.Risk.Recommendations);
        Assert.Contains(RiskSummaryBuilder.ReviewNoBaselineReason, body.Risk.Reasons);
        Assert.Equal(1, body.Coverage.CoveredOperations);
        Assert.Equal(100m, body.Coverage.CoveragePercent);
    }

    [Fact]
    public async Task Run_BaselineValidationRegression_ReturnsBlockAndDoesNotReplaceBaseline()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, Spec);
        _factory.Runner.ResultFactory = test => Succeed(test, "[]");
        await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var original = await FindBaselineAsync(project.Id);
        Assert.NotNull(original);

        _factory.Runner.ResultFactory = null;
        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(nameof(RegressionDecision.Block), body.Decision);
        Assert.Equal(original.EstablishedAt, body.BaselineEstablishedAt);
        var finding = Assert.Single(body.Findings);
        Assert.Equal(RegressionDetector.ValidationRegressed, finding.Code);
        Assert.Equal(nameof(RegressionFindingCategory.Regression), finding.Category);
        Assert.Equal("/pets", finding.Path);
        Assert.Equal(nameof(ContractValidationOutcome.Failed), Assert.Single(body.Results).Validation.Outcome);
        Assert.Equal(nameof(RiskSummaryLevel.High), body.Risk.Level);
        Assert.Equal(RiskSummaryBuilder.BlockTitle, body.Risk.Title);
        Assert.Equal(RiskSummaryBuilder.BlockSummary, body.Risk.Summary);
        Assert.Equal(RiskSummaryBuilder.BlockRecommendations, body.Risk.Recommendations);
        Assert.Contains(body.Risk.Reasons, reason => reason.Contains("Previously passing validation now fails for GET /pets.", StringComparison.Ordinal));
        Assert.Equal(nameof(RegressionDecision.Block), body.Decision);

        var unchanged = await FindBaselineAsync(project.Id);
        Assert.NotNull(unchanged);
        Assert.Equal(original.Id, unchanged.Id);
        Assert.Equal(original.EstablishedAt, unchanged.EstablishedAt);
        Assert.Equal(original.SnapshotJson, unchanged.SnapshotJson);
        var lastRun = await FindLastRunAsync(project.Id);
        Assert.NotNull(lastRun);
        Assert.Equal(nameof(RegressionDecision.Block), lastRun.Decision);
        Assert.Equal(1, lastRun.FailedCount);
        Assert.Equal(1, lastRun.FindingCount);
    }

    [Fact]
    public async Task Run_BaselineExecutionRegression_ReturnsBlockAndDoesNotReplaceBaseline()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, Spec);
        _factory.Runner.ResultFactory = test => Succeed(test, "[]");
        await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var original = await FindBaselineAsync(project.Id);
        Assert.NotNull(original);

        _factory.Runner.ResultFactory = test => Error(test, "The request timed out.");
        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(RegressionDecision.Block), body.Decision);
        Assert.Equal(RegressionDetector.ExecutionRegressed, Assert.Single(body.Findings).Code);
        Assert.Equal(nameof(ContractValidationOutcome.Error), Assert.Single(body.Results).Validation.Outcome);
        Assert.Equal(original.Id, (await FindBaselineAsync(project.Id))!.Id);
        Assert.Equal(1, (await FindLastRunAsync(project.Id))!.ErrorCount);
    }

    [Fact]
    public async Task Run_ContractDriftWhilePassing_ReportsFindingAndReplacesBaseline()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, Spec);
        _factory.Runner.ResultFactory = test => Succeed(test, "[]");
        await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var original = await FindBaselineAsync(project.Id);
        Assert.NotNull(original);

        await ImportAsync(project.Id, ObjectSpec);
        _factory.Runner.ResultFactory = test => Succeed(test, "{}");
        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(nameof(RegressionDecision.Review), body.Decision);
        Assert.Equal(nameof(ContractValidationOutcome.Passed), Assert.Single(body.Results).Validation.Outcome);
        var finding = Assert.Single(body.Findings);
        Assert.Equal(RegressionDetector.SchemaTypeChanged, finding.Code);
        Assert.Equal(nameof(RegressionFindingCategory.ContractDrift), finding.Category);
        Assert.Equal("/pets", finding.Path);
        Assert.Equal(nameof(RiskSummaryLevel.Medium), body.Risk.Level);
        Assert.Equal(RiskSummaryBuilder.ReviewTitle, body.Risk.Title);
        Assert.Equal(RiskSummaryBuilder.ReviewSummary, body.Risk.Summary);
        Assert.Equal(RiskSummaryBuilder.ReviewRecommendations, body.Risk.Recommendations);
        Assert.Contains("Response schema changed for GET /pets.", body.Risk.Reasons);
        Assert.Equal(nameof(RegressionDecision.Review), body.Decision);

        var replaced = await FindBaselineAsync(project.Id);
        Assert.NotNull(replaced);
        Assert.Equal(original.Id, replaced.Id);
        Assert.NotEqual(original.SnapshotJson, replaced.SnapshotJson);
        Assert.Contains("object", replaced.SnapshotJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_MultipleTests_EachHasValidationAndFindingsMapToTheFailingTest()
    {
        var project = await CreateProjectAsync("https://pets.example.com");
        await ImportAsync(project.Id, TwoOperationSpec);
        _factory.Runner.ResultFactory = test => test.Path == "/pets"
            ? Succeed(test, "[]")
            : Succeed(test, """{"ok":true}""");
        await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        _factory.Runner.ResultFactory = test => test.Path == "/pets"
            ? Succeed(test, "{}")
            : Succeed(test, """{"ok":true}""");
        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(2, body.Results.Count);
        Assert.Equal(nameof(ContractValidationOutcome.Failed), Assert.Single(body.Results, result => result.Path == "/pets").Validation.Outcome);
        Assert.Equal(nameof(ContractValidationOutcome.Passed), Assert.Single(body.Results, result => result.Path == "/health").Validation.Outcome);
        Assert.Equal(nameof(RegressionDecision.Block), body.Decision);
        var finding = Assert.Single(body.Findings);
        Assert.Equal(RegressionDetector.ValidationRegressed, finding.Code);
        Assert.Equal("/pets", finding.Path);
        Assert.DoesNotContain(body.Findings, item => item.Path == "/health");

        var lastRun = await FindLastRunAsync(project.Id);
        Assert.NotNull(lastRun);
        var persisted = RegressionPersistenceJson.DeserializeResults(lastRun.ResultsJson);
        Assert.Equal(2, persisted.Tests.Count);
        Assert.Equal("/pets", Assert.Single(persisted.Findings).Path);
        Assert.DoesNotContain("duration", lastRun.ResultsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("headers", lastRun.ResultsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_UnknownProject_Returns404()
    {
        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(Guid.NewGuid()), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Project not found", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_ProjectWithoutSpec_Returns404()
    {
        var project = await CreateProjectAsync("https://pets.example.com");

        var response = await _client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("OpenAPI specification not found", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private async Task<ProjectResponse> CreateProjectAsync(string baseUrl)
    {
        var response = await _client.PostAsJsonAsync(
            ProjectsEndpoint.Route,
            new { name = "Pets API", baseUrl });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        return project;
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

    private static ApiTestExecutionResult Error(GeneratedApiTestCase test, string error) =>
        new(
            test.Kind,
            test.Method,
            test.Path,
            test.SourceMethod,
            test.SourcePath,
            test.ExpectedStatus,
            actualStatus: null,
            new Dictionary<string, string>(),
            body: null,
            durationMs: 1,
            statusMatched: false,
            error);

    private async Task ImportAsync(Guid projectId, string specification)
    {
        var json = JsonSerializer.Serialize(new { specification });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(SpecsImportEndpoint.RouteFor(projectId), content);
        response.EnsureSuccessStatusCode();
    }

    private static string MiniOpenApiSpec(string title, string version, string path) =>
        $$"""
        {
          "openapi": "3.0.3",
          "info": { "title": "{{title}}", "version": "{{version}}" },
          "paths": {
            "{{path}}": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
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

    private static string EmptyPathsDocument(string title) =>
        $$"""
        {
          "openapi": "3.0.3",
          "info": { "title": "{{title}}", "version": "9.0.0" },
          "paths": {}
        }
        """;

    private async Task<ProjectBaselineRecord?> FindBaselineAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProjectBaselineStore>()
            .FindByProjectIdAsync(projectId);
    }

    private async Task<IReadOnlyList<RegressionTestSnapshot>> GetBaselineSnapshotsAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProjectBaselineStore>()
            .GetSnapshotsAsync(projectId);
    }

    private async Task<ProjectTestRunRecord?> FindLastRunAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProjectTestRunStore>()
            .FindByProjectIdAsync(projectId);
    }

    private async Task<int> CountBaselinesAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProjectBaselines
            .CountAsync(record => record.ProjectId == projectId);
    }

    private async Task<int> CountLastRunsAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProjectTestRuns
            .CountAsync(record => record.ProjectId == projectId);
    }
}

public sealed class StubApiTestRunner : IApiTestRunner
{
    public string? LastBaseUrl { get; private set; }

    public IReadOnlyList<GeneratedApiTestCase>? LastTests { get; private set; }

    public Func<GeneratedApiTestCase, ApiTestExecutionResult>? ResultFactory { get; set; }

    public void Reset()
    {
        LastBaseUrl = null;
        LastTests = null;
        ResultFactory = null;
    }

    public Task<IReadOnlyList<ApiTestExecutionResult>> RunAsync(
        string baseUrl,
        IReadOnlyList<GeneratedApiTestCase> tests,
        CancellationToken cancellationToken = default)
    {
        LastBaseUrl = baseUrl;
        LastTests = tests;
        var factory = ResultFactory ?? DefaultResult;
        IReadOnlyList<ApiTestExecutionResult> results = tests.Select(factory).ToList();
        return Task.FromResult(results);
    }

    private static ApiTestExecutionResult DefaultResult(GeneratedApiTestCase test) =>
        new(
            test.Kind,
            test.Method,
            test.Path,
            test.SourceMethod,
            test.SourcePath,
            test.ExpectedStatus,
            test.ExpectedStatus,
            new Dictionary<string, string>(),
            body: "{}",
            durationMs: 1,
            statusMatched: true,
            error: null);
}

public class TestsRunWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"testshield-run-{Guid.NewGuid():N}.db");

    public StubApiTestRunner Runner => (StubApiTestRunner)Services.GetRequiredService<IApiTestRunner>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TestShield", $"Data Source={_dbPath}");
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IApiTestRunner)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IApiTestRunner, StubApiTestRunner>();
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
