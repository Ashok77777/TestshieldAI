using System.Text.Json;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class ApiTestGeneratorTests
{
    private readonly IApiTestGenerator _generator = new ApiTestGenerator();

    [Fact]
    public void Generate_SimpleGet_ProducesOneHappyPathCase()
    {
        var operation = Operation(
            "GET",
            "/pets",
            parameters: [],
            requestBody: null,
            responses: Response("200", ObjectSchema()));

        var cases = _generator.Generate([operation]);

        var generated = Assert.Single(cases);
        Assert.Equal(GeneratedApiTestKind.HappyPath, generated.Kind);
        Assert.Equal("GET", generated.Method);
        Assert.Equal("/pets", generated.PathTemplate);
        Assert.Equal("/pets", generated.Path);
        Assert.Empty(generated.Parameters);
        Assert.Null(generated.RequestBody);
        Assert.Equal(200, generated.ExpectedStatus);
        Assert.Equal("GET", generated.SourceMethod);
        Assert.Equal("/pets", generated.SourcePath);
        Assert.Null(generated.SpecKey);
    }

    [Fact]
    public void Generate_CopiesSpecKeyAndKeepsSourcePathAsIdentityPath()
    {
        var operation = Operation(
            "GET",
            "/pets/{id}",
            [Param("id", "path", required: true, IntegerSchema())],
            requestBody: null,
            responses: Response("200", ObjectSchema()),
            specKey: "Customer");

        var generated = Assert.Single(_generator.Generate([operation]));

        Assert.Equal("Customer", generated.SpecKey);
        Assert.Equal("/pets/{id}", generated.SourcePath);
        Assert.Equal("/pets/1", generated.Path);
    }

    [Fact]
    public void Generate_SameMethodAndPath_DifferentSpecKeys_RemainDistinct()
    {
        var customer = Operation("GET", "/pets", [], null, Response("200", ObjectSchema()), "Customer");
        var order = Operation("GET", "/pets", [], null, Response("200", ObjectSchema()), "Order");

        var cases = _generator.Generate([customer, order]);

        Assert.Equal(2, cases.Count);
        Assert.Equal("Customer", cases[0].SpecKey);
        Assert.Equal("Order", cases[1].SpecKey);
        Assert.Equal("/pets", cases[0].SourcePath);
        Assert.Equal("/pets", cases[1].SourcePath);
    }

    [Fact]
    public void Generate_MultipleOperations_PreservesStableOrderWithHappyPathBeforeNegative()
    {
        var getPets = Operation("GET", "/pets", [], null, Response("200", ObjectSchema()));
        var createPet = Operation(
            "POST",
            "/pets",
            [],
            ObjectSchema(required: ["name"], properties: new Dictionary<string, ImportedSchema>
            {
                ["name"] = StringSchema()
            }),
            Response("201", ObjectSchema()));

        var cases = _generator.Generate([getPets, createPet]);

        Assert.Equal(3, cases.Count);
        Assert.Equal("GET", cases[0].Method);
        Assert.Equal("/pets", cases[0].SourcePath);
        Assert.Equal(GeneratedApiTestKind.HappyPath, cases[0].Kind);
        Assert.Equal("POST", cases[1].Method);
        Assert.Equal(GeneratedApiTestKind.HappyPath, cases[1].Kind);
        Assert.Equal("POST", cases[2].Method);
        Assert.Equal(GeneratedApiTestKind.NegativeMissingRequiredBody, cases[2].Kind);
    }

    [Fact]
    public void Generate_RequiredPathParameter_SubstitutesPlaceholderIntoPath()
    {
        var operation = Operation(
            "GET",
            "/pets/{id}",
            [Param("id", "path", required: true, IntegerSchema())],
            requestBody: null,
            responses: Response("200", ObjectSchema()));

        var generated = Assert.Single(_generator.Generate([operation]));

        Assert.Equal("/pets/{id}", generated.PathTemplate);
        Assert.Equal("/pets/1", generated.Path);
        var parameter = Assert.Single(generated.Parameters);
        Assert.Equal("id", parameter.Name);
        Assert.Equal("path", parameter.Location);
        Assert.True(parameter.Required);
        Assert.Equal("1", parameter.Placeholder);
    }

    [Fact]
    public void Generate_QueryParameters_IncludesRequiredAndOmitsOptional()
    {
        var operation = Operation(
            "GET",
            "/pets",
            [
                Param("limit", "query", required: true, IntegerSchema()),
                Param("q", "query", required: false, StringSchema())
            ],
            requestBody: null,
            responses: Response("200", ObjectSchema()));

        var generated = Assert.Single(_generator.Generate([operation]));

        Assert.Equal("/pets", generated.Path);
        var parameter = Assert.Single(generated.Parameters);
        Assert.Equal("limit", parameter.Name);
        Assert.Equal("query", parameter.Location);
        Assert.Equal("1", parameter.Placeholder);
    }

    [Fact]
    public void Generate_RequiredRequestBody_CreatesValidHappyPathJson()
    {
        var body = ObjectSchema(
            required: ["name", "age"],
            properties: new Dictionary<string, ImportedSchema>
            {
                ["name"] = StringSchema(),
                ["age"] = IntegerSchema(),
                ["nick"] = StringSchema()
            });
        var operation = Operation("POST", "/pets", [], body, Response("201", ObjectSchema()));

        var generated = _generator.Generate([operation])[0];

        Assert.Equal(GeneratedApiTestKind.HappyPath, generated.Kind);
        Assert.NotNull(generated.RequestBody);
        using var json = JsonDocument.Parse(generated.RequestBody);
        Assert.Equal("a", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("age").GetInt32());
        Assert.False(json.RootElement.TryGetProperty("nick", out _));
    }

    [Fact]
    public void Generate_RequiredRequestBody_CreatesOneNegativeCaseOmittingFirstRequiredProperty()
    {
        var body = ObjectSchema(
            required: ["name", "age"],
            properties: new Dictionary<string, ImportedSchema>
            {
                ["name"] = StringSchema(),
                ["age"] = IntegerSchema()
            });
        var operation = Operation("POST", "/pets", [], body, Response("201", ObjectSchema()));

        var cases = _generator.Generate([operation]);

        Assert.Equal(2, cases.Count);
        var negative = cases[1];
        Assert.Equal(GeneratedApiTestKind.NegativeMissingRequiredBody, negative.Kind);
        Assert.NotNull(negative.RequestBody);
        using var json = JsonDocument.Parse(negative.RequestBody);
        Assert.False(json.RootElement.TryGetProperty("name", out _));
        Assert.Equal(1, json.RootElement.GetProperty("age").GetInt32());
        Assert.Equal(400, negative.ExpectedStatus);
    }

    [Fact]
    public void Generate_OptionalOnlyRequestBody_DoesNotCreateNegativeCase()
    {
        var body = ObjectSchema(
            required: [],
            properties: new Dictionary<string, ImportedSchema>
            {
                ["nick"] = StringSchema()
            });
        var operation = Operation("POST", "/pets", [], body, Response("201", ObjectSchema()));

        var generated = Assert.Single(_generator.Generate([operation]));

        Assert.Equal(GeneratedApiTestKind.HappyPath, generated.Kind);
        Assert.Equal("{}", generated.RequestBody);
    }

    [Fact]
    public void Generate_MultipleResponseStatuses_SelectsLowest2xx()
    {
        var responses = new Dictionary<string, ImportedSchema?>
        {
            ["400"] = ObjectSchema(),
            ["201"] = ObjectSchema(),
            ["200"] = ObjectSchema(),
            ["default"] = ObjectSchema()
        };
        var operation = Operation("GET", "/pets", [], null, responses);

        var generated = Assert.Single(_generator.Generate([operation]));

        Assert.Equal(200, generated.ExpectedStatus);
        Assert.NotNull(generated.ExpectedResponseSchema);
    }

    [Fact]
    public void Generate_204Response_AllowsNullExpectedResponseSchema()
    {
        var operation = Operation(
            "DELETE",
            "/pets/{id}",
            [Param("id", "path", required: true, IntegerSchema())],
            requestBody: null,
            responses: new Dictionary<string, ImportedSchema?> { ["204"] = null });

        var generated = Assert.Single(_generator.Generate([operation]));

        Assert.Equal(204, generated.ExpectedStatus);
        Assert.Null(generated.ExpectedResponseSchema);
    }

    [Fact]
    public void Generate_EmptyOperations_ReturnsEmptyList()
    {
        Assert.Empty(_generator.Generate([]));
    }

    [Fact]
    public void Generate_EnumParameter_UsesFirstEnumValue()
    {
        var schema = new ImportedSchema(
            "string",
            [],
            new Dictionary<string, ImportedSchema>(),
            ["active", "inactive"],
            null);
        var operation = Operation(
            "GET",
            "/pets/{status}",
            [Param("status", "path", required: true, schema)],
            requestBody: null,
            responses: Response("200", ObjectSchema()));

        var generated = Assert.Single(_generator.Generate([operation]));

        Assert.Equal("active", Assert.Single(generated.Parameters).Placeholder);
        Assert.Equal("/pets/active", generated.Path);
    }

    [Fact]
    public void Generate_NoNumericResponses_UsesFallback200()
    {
        var operation = Operation(
            "GET",
            "/ping",
            [],
            null,
            new Dictionary<string, ImportedSchema?> { ["default"] = ObjectSchema() });

        var generated = Assert.Single(_generator.Generate([operation]));

        Assert.Equal(ApiTestGenerator.FallbackExpectedStatus, generated.ExpectedStatus);
        Assert.NotNull(generated.ExpectedResponseSchema);
    }

    private static ImportedOperation Operation(
        string method,
        string path,
        IReadOnlyList<ImportedParameter> parameters,
        ImportedSchema? requestBody,
        IReadOnlyDictionary<string, ImportedSchema?> responses,
        string? specKey = null) =>
        new(method, path, parameters, requestBody, responses, specKey);

    private static ImportedParameter Param(string name, string location, bool required, ImportedSchema? schema) =>
        new(name, location, required, schema);

    private static Dictionary<string, ImportedSchema?> Response(string status, ImportedSchema? schema) =>
        new() { [status] = schema };

    private static ImportedSchema ObjectSchema(
        IReadOnlyList<string>? required = null,
        IReadOnlyDictionary<string, ImportedSchema>? properties = null) =>
        new(
            "object",
            required ?? [],
            properties ?? new Dictionary<string, ImportedSchema>(),
            [],
            null);

    private static ImportedSchema StringSchema() =>
        new("string", [], new Dictionary<string, ImportedSchema>(), [], null);

    private static ImportedSchema IntegerSchema() =>
        new("integer", [], new Dictionary<string, ImportedSchema>(), [], null);
}
