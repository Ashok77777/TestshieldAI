using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Endpoints;
using TestShieldAI.Api.Persistence;

namespace TestShieldAI.Engine.Tests;

public class SpecsImportEndpointTests : IClassFixture<SpecsImportWebApplicationFactory>
{
    private const string ValidOpenApi3Spec = """
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
                  "content": {
                    "application/json": {
                      "schema": { "type": "object" }
                    }
                  }
                },
                "responses": {
                  "201": {
                    "description": "Created"
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

    private readonly SpecsImportWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SpecsImportEndpointTests(SpecsImportWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Import_ValidOpenApiSpec_Returns200()
    {
        var project = await CreateProjectAsync();
        var response = await ImportAsync(project.Id, ValidOpenApi3Spec);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ImportSpecResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Operations);
    }

    [Fact]
    public async Task Import_ValidOpenApiSpec_ReturnsMultipleOperations()
    {
        var project = await CreateProjectAsync();
        var response = await ImportAsync(project.Id, ValidOpenApi3Spec);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ImportSpecResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body.Operations.Count >= 2);
        Assert.Contains(body.Operations, operation => operation.Method == "GET" && operation.Path == "/pets/{id}");
        Assert.Contains(body.Operations, operation => operation.Method == "POST" && operation.Path == "/pets");
    }

    [Fact]
    public async Task Import_SameSpecKeyTwice_PersistsANewHistoryRow()
    {
        var project = await CreateProjectAsync();
        var first = await ImportAsync(project.Id, ValidOpenApi3Spec);
        first.EnsureSuccessStatusCode();
        await Task.Delay(25);
        var v2 = ValidOpenApi3Spec.Replace("\"1.0.0\"", "\"2.0.0\"", StringComparison.Ordinal);
        var second = await ImportAsync(project.Id, v2);
        second.EnsureSuccessStatusCode();

        var stored = (await StoredSpecificationsAsync())
            .Where(record => record.ProjectId == project.Id)
            .OrderBy(record => record.ImportedAt)
            .ToList();
        Assert.Equal(2, stored.Count);
        Assert.Equal(ValidOpenApi3Spec, stored[0].RawSpecification);
        Assert.Equal(v2, stored[1].RawSpecification);
        Assert.True(stored[1].ImportedAt >= stored[0].ImportedAt);
    }

    [Fact]
    public async Task Import_ValidSpecification_PersistsOriginalRawTextForProject()
    {
        var project = await CreateProjectAsync("Persistence API", "https://persist.example.com");
        const string marker = "title\": \"Persistence Marker Pets\"";
        var spec = ValidOpenApi3Spec.Replace("title\": \"Pets\"", marker, StringComparison.Ordinal);

        var response = await ImportAsync(project.Id, spec);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredSpecificationsAsync();
        Assert.Contains(stored, record =>
            record.RawSpecification == spec &&
            record.ProjectId == project.Id);
        Assert.DoesNotContain(stored, record => record.RawSpecification.Contains("ImportedOperation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_UnknownProject_Returns404ProblemDetails()
    {
        var unknownId = Guid.NewGuid();
        var before = (await StoredSpecificationsAsync()).Count;

        var response = await ImportAsync(unknownId, ValidOpenApi3Spec);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("Project not found", payload, StringComparison.Ordinal);
        var after = await StoredSpecificationsAsync();
        Assert.Equal(before, after.Count);
        Assert.DoesNotContain(after, record => record.ProjectId == unknownId);
    }

    [Fact]
    public async Task Import_InvalidSpecification_IsNotPersisted()
    {
        var project = await CreateProjectAsync();
        var before = (await StoredSpecificationsAsync()).Count;

        var response = await ImportAsync(project.Id, "this is not a valid OpenAPI document");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var after = await StoredSpecificationsAsync();
        Assert.Equal(before, after.Count);
        Assert.DoesNotContain(after, record => record.RawSpecification == "this is not a valid OpenAPI document");
    }

    [Fact]
    public async Task Import_InvalidSpecification_Returns400ProblemDetails()
    {
        var project = await CreateProjectAsync();
        var response = await ImportAsync(project.Id, "this is not a valid OpenAPI document");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid OpenAPI specification", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft.OpenApi.Readers", payload, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Import_MissingOrBlankSpecification_Returns400ProblemDetails(string? specification)
    {
        var project = await CreateProjectAsync();
        var before = (await StoredSpecificationsAsync()).Count;

        HttpResponseMessage response;
        if (specification is null)
        {
            response = await _client.PostAsJsonAsync(SpecsImportEndpoint.RouteFor(project.Id), new { });
        }
        else
        {
            response = await ImportAsync(project.Id, specification);
        }

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid OpenAPI specification", payload, StringComparison.Ordinal);
        Assert.Equal(before, (await StoredSpecificationsAsync()).Count);
    }

    [Fact]
    public async Task Import_OldUnscopedRoute_IsNotAvailable()
    {
        var response = await _client.PostAsJsonAsync("/api/specs/import", new { specification = ValidOpenApi3Spec });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<ProjectResponse> CreateProjectAsync(
        string name = "Pets API",
        string baseUrl = "https://pets.example.com")
    {
        var response = await _client.PostAsJsonAsync(ProjectsEndpoint.Route, new { name, baseUrl });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        return project;
    }

    private async Task<HttpResponseMessage> ImportAsync(Guid projectId, string specification)
    {
        var json = JsonSerializer.Serialize(new { specification });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync(SpecsImportEndpoint.RouteFor(projectId), content);
    }

    private async Task<List<OpenApiSpecificationRecord>> StoredSpecificationsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OpenApiSpecifications.AsNoTracking().ToListAsync();
    }
}

public class SpecsImportWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"testshield-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TestShield", $"Data Source={_dbPath}");
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
