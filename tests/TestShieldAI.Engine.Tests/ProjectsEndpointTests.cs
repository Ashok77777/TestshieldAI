using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Endpoints;

namespace TestShieldAI.Engine.Tests;

public class ProjectsEndpointTests : IClassFixture<SpecsImportWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;

    public ProjectsEndpointTests(SpecsImportWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateProject_Returns201WithProjectInformation()
    {
        var response = await _client.PostAsJsonAsync(
            ProjectsEndpoint.Route,
            new { name = "Orders API", baseUrl = "https://orders.example.com" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("Orders API", project.Name);
        Assert.Equal("https://orders.example.com", project.BaseUrl);
        Assert.NotEqual(default, project.CreatedAt);
        Assert.Contains(project.Id.ToString(), response.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateProject_CanCreateMultipleProjects()
    {
        var first = await CreateProjectAsync("Orders API", "https://orders.example.com");
        var second = await CreateProjectAsync("Payments API", "https://payments.example.com");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("Orders API", first.Name);
        Assert.Equal("Payments API", second.Name);
        Assert.Equal("https://orders.example.com", first.BaseUrl);
        Assert.Equal("https://payments.example.com", second.BaseUrl);
    }

    [Fact]
    public async Task ListProjects_ReturnsConfiguredProjects()
    {
        var created = await CreateProjectAsync("Inventory API", "https://inventory.example.com");

        var response = await _client.GetAsync(ProjectsEndpoint.Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectResponse>>(JsonOptions);
        Assert.NotNull(projects);
        Assert.Contains(projects, project =>
            project.Id == created.Id &&
            project.Name == "Inventory API" &&
            project.BaseUrl == "https://inventory.example.com");
    }

    private async Task<ProjectResponse> CreateProjectAsync(string name, string baseUrl)
    {
        var response = await _client.PostAsJsonAsync(ProjectsEndpoint.Route, new { name, baseUrl });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        return project;
    }
}
