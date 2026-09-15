using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TestShieldAI.DemoApi.Tests;

public class PetsApiTests : IClassFixture<DemoApiWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;

    public PetsApiTests(DemoApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPets_Returns200WithSeededPets()
    {
        var response = await _client.GetAsync("/pets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pets = await response.Content.ReadFromJsonAsync<List<Pet>>(JsonOptions);
        Assert.NotNull(pets);
        Assert.Contains(pets, pet => pet.Id == 1 && pet.Name == "Buddy" && pet.Species == "dog");
        Assert.Contains(pets, pet => pet.Id == 2 && pet.Name == "Milo" && pet.Species == "cat");
        Assert.Contains(pets, pet => pet.Id == 3 && pet.Name == "Coco" && pet.Species == "bird");
    }

    [Fact]
    public async Task GetPetById_Existing_Returns200()
    {
        var response = await _client.GetAsync("/pets/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pet = await response.Content.ReadFromJsonAsync<Pet>(JsonOptions);
        Assert.NotNull(pet);
        Assert.Equal(1, pet.Id);
        Assert.Equal("Buddy", pet.Name);
        Assert.Equal("dog", pet.Species);
    }

    [Fact]
    public async Task GetPetById_Missing_Returns404()
    {
        var response = await _client.GetAsync("/pets/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatePet_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/pets", new { name = "Nala", species = "cat" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var pet = await response.Content.ReadFromJsonAsync<Pet>(JsonOptions);
        Assert.NotNull(pet);
        Assert.True(pet.Id >= 4);
        Assert.Equal("Nala", pet.Name);
        Assert.Equal("cat", pet.Species);
        Assert.Equal($"/pets/{pet.Id}", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"species":"dog"}""")]
    [InlineData("""{"name":"Buddy"}""")]
    [InlineData("""{"name":"","species":"dog"}""")]
    [InlineData("""{"name":"Buddy","species":""}""")]
    public async Task CreatePet_MissingRequiredFields_Returns400(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/pets", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePet_OmittingName_ReturnsJsonValidationProblem()
    {
        using var content = new StringContent("""{"species":"a"}""", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/pets", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.True(document.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task OpenApi_DescribesPetsEndpoints()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/pets", out var pets));
        Assert.True(pets.TryGetProperty("get", out _));
        Assert.True(pets.TryGetProperty("post", out var post));
        Assert.True(paths.TryGetProperty("/pets/{id}", out var byId));
        Assert.True(byId.TryGetProperty("get", out var getById));

        var idParameter = getById.GetProperty("parameters")[0];
        Assert.Equal("id", idParameter.GetProperty("name").GetString());
        Assert.Equal("path", idParameter.GetProperty("in").GetString());
        Assert.True(idParameter.GetProperty("required").GetBoolean());
        Assert.Equal("integer", idParameter.GetProperty("schema").GetProperty("type").GetString());

        var requestSchema = ResolveSchema(
            document.RootElement,
            post.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        var required = requestSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToList();
        Assert.Contains("name", required);
        Assert.Contains("species", required);
    }

    private static JsonElement ResolveSchema(JsonElement root, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
        {
            return schema;
        }

        var pointer = reference.GetString() ?? "";
        const string prefix = "#/components/schemas/";
        Assert.StartsWith(prefix, pointer);
        return root.GetProperty("components").GetProperty("schemas").GetProperty(pointer[prefix.Length..]);
    }
}
