using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Endpoints;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class TestsGenerateEndpointTests : IClassFixture<SpecsImportWebApplicationFactory>
{
    private const string SpecWithPathAndRequiredBody = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets/{id}": {
              "get": {
                "parameters": [
                  {
                    "name": "id",
                    "in": "path",
                    "required": true,
                    "schema": { "type": "integer" }
                  }
                ],
                "responses": {
                  "200": {
                    "description": "A pet",
                    "content": {
                      "application/json": {
                        "schema": { "type": "object" }
                      }
                    }
                  }
                }
              }
            },
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
                    "description": "Created",
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

    private readonly HttpClient _client;

    public TestsGenerateEndpointTests(SpecsImportWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Generate_AfterImport_Returns200WithMultipleTests()
    {
        var project = await CreateProjectAsync();
        await ImportAsync(project.Id, SpecWithPathAndRequiredBody);

        var response = await _client.PostAsync(TestsGenerateEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GenerateTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body.Tests.Count >= 2);
        Assert.Contains(body.Tests, test =>
            test.Kind == "HappyPath" && test.Method == "GET" && test.Path == "/pets/1");
        Assert.Contains(body.Tests, test =>
            test.Kind == "HappyPath" && test.Method == "POST" && test.SourcePath == "/pets");
        Assert.Contains(body.Tests, test => test.Kind == "NegativeMissingRequiredBody");
        Assert.All(body.Tests, test => Assert.Equal("Pets", test.SpecKey));
        Assert.Equal(2, body.Coverage.TotalOperations);
        Assert.Equal(2, body.Coverage.CoveredOperations);
        Assert.Equal(0, body.Coverage.UncoveredOperations);
        Assert.Equal(100m, body.Coverage.CoveragePercent);
        Assert.Equal(0, body.Coverage.AiCoveredOperations);
        Assert.True(body.Coverage.Scenarios.HappyPath >= 2);
        Assert.True(body.Coverage.Scenarios.Negative >= 1);
        Assert.Equal(0, body.Coverage.Scenarios.AiPositive);
        Assert.Empty(body.Coverage.Gaps);
        Assert.Equal(nameof(RiskSummaryLevel.Low), body.Risk.Level);
        Assert.Equal(RiskSummaryBuilder.SafeTitle, body.Risk.Title);
        Assert.Empty(body.Risk.Recommendations);
        Assert.NotEmpty(body.Risk.Reasons);
        Assert.DoesNotContain(body.Risk.Reasons, reason => reason.Contains("coverage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Generate_LatestPerSpecKey_MergesCurrentSpecificationsAndIgnoresOlderSameTitle()
    {
        var project = await CreateProjectAsync();
        await ImportAsync(project.Id, Spec("Pets", "1.0.0", "/legacy"));
        await Task.Delay(25);
        await ImportAsync(project.Id, Spec("Order", "1.0.0", "/orders"));
        await Task.Delay(25);
        await ImportAsync(project.Id, Spec("Pets", "2.0.0", "/pets"));

        var response = await _client.PostAsync(TestsGenerateEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GenerateTestsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Contains(body.Tests, test => test.SpecKey == "Pets" && test.SourcePath == "/pets");
        Assert.Contains(body.Tests, test => test.SpecKey == "Order" && test.SourcePath == "/orders");
        Assert.DoesNotContain(body.Tests, test => test.SourcePath == "/legacy");
    }

    [Fact]
    public async Task Generate_SameMethodAndPath_DifferentSpecKeys_AreDistinctTests()
    {
        var project = await CreateProjectAsync();
        await ImportAsync(project.Id, Spec("Customer", "1.0.0", "/pets"));
        await Task.Delay(25);
        await ImportAsync(project.Id, Spec("Order", "1.0.0", "/pets"));

        var response = await _client.PostAsync(TestsGenerateEndpoint.RouteFor(project.Id), null);
        var body = await response.Content.ReadFromJsonAsync<GenerateTestsResponse>(JsonOptions);

        Assert.NotNull(body);
        Assert.Equal(2, body.Tests.Count);
        Assert.Contains(body.Tests, test => test.SpecKey == "Customer" && test.SourceMethod == "GET" && test.SourcePath == "/pets");
        Assert.Contains(body.Tests, test => test.SpecKey == "Order" && test.SourceMethod == "GET" && test.SourcePath == "/pets");
        Assert.Equal(2, body.Coverage.TotalOperations);
        Assert.Equal(2, body.Coverage.CoveredOperations);
        Assert.Equal(100m, body.Coverage.CoveragePercent);
        Assert.Empty(body.Coverage.Gaps);
    }

    [Fact]
    public async Task Generate_Coverage_DoesNotIncludeExecutionOrPersistenceFields()
    {
        var project = await CreateProjectAsync();
        await ImportAsync(project.Id, Spec("Pets", "1.0.0", "/pets"));

        var response = await _client.PostAsync(TestsGenerateEndpoint.RouteFor(project.Id), null);
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<GenerateTestsResponse>(json, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(1, body.Coverage.TotalOperations);
        Assert.Equal(100m, body.Coverage.CoveragePercent);
        Assert.Equal(nameof(RiskSummaryLevel.Low), body.Risk.Level);
        Assert.Empty(body.Risk.Recommendations);
        Assert.NotEmpty(body.Risk.Reasons);
        Assert.DoesNotContain("baseUrl", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("decision", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actualStatus", json, StringComparison.OrdinalIgnoreCase);
        var gapJson = JsonSerializer.Serialize(body.Coverage.Gaps);
        Assert.DoesNotContain("requestBody", gapJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("headers", gapJson, StringComparison.OrdinalIgnoreCase);
    }

    private static string Spec(string title, string version, string path) =>
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

    [Fact]
    public async Task Generate_DoesNotIncludeBaseUrl()
    {
        var project = await CreateProjectAsync();
        await ImportAsync(project.Id, SpecWithPathAndRequiredBody);

        var response = await _client.PostAsync(TestsGenerateEndpoint.RouteFor(project.Id), null);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("baseUrl", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generate_UnknownProject_Returns404()
    {
        var response = await _client.PostAsync(TestsGenerateEndpoint.RouteFor(Guid.NewGuid()), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("Project not found", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_ProjectWithoutSpec_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsync(TestsGenerateEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("OpenAPI specification not found", payload, StringComparison.Ordinal);
    }

    private async Task<ProjectResponse> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync(
            ProjectsEndpoint.Route,
            new { name = "Pets API", baseUrl = "https://pets.example.com" });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        return project;
    }

    private async Task ImportAsync(Guid projectId, string specification)
    {
        var json = JsonSerializer.Serialize(new { specification });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(SpecsImportEndpoint.RouteFor(projectId), content);
        response.EnsureSuccessStatusCode();
    }
}
