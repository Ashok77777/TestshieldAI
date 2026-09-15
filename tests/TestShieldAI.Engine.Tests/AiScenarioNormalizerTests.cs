using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class AiScenarioNormalizerTests
{
    private readonly IAiScenarioNormalizer _normalizer = new AiScenarioNormalizer();
    private readonly IAiScenarioGenerator _noOp = new NoOpAiScenarioGenerator();

    [Fact]
    public async Task NoOpGenerator_ReturnsNoScenariosAndWarning()
    {
        var result = await _noOp.GenerateAsync([GetPets()], []);

        Assert.False(result.Succeeded);
        Assert.Empty(result.Scenarios);
        Assert.Contains(NoOpAiScenarioGenerator.NotConfiguredWarning, result.Warnings);
    }

    [Fact]
    public void Normalize_ValidPositive_IsAcceptedAsAiPositive()
    {
        var operation = GetPets();
        var result = _normalizer.Normalize([Proposal(AiScenarioType.Positive, operation)], [operation], []);

        var test = Assert.Single(result.Tests);
        Assert.Equal(GeneratedApiTestKind.AiPositive, test.Kind);
        Assert.Equal("GET", test.SourceMethod);
        Assert.Equal("/pets", test.SourcePath);
        Assert.Equal(200, test.ExpectedStatus);
        Assert.Equal("array", test.ExpectedResponseSchema?.Type);
        Assert.Equal("Happy path list.", test.Rationale);
        Assert.False(string.IsNullOrWhiteSpace(test.ScenarioKey));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Normalize_ValidNegative_IsAcceptedAsAiNegative()
    {
        var operation = PostPets();
        var result = _normalizer.Normalize(
            [Proposal(AiScenarioType.Negative, operation, body: """{"name":""}""")],
            [operation],
            []);

        var test = Assert.Single(result.Tests);
        Assert.Equal(GeneratedApiTestKind.AiNegative, test.Kind);
        Assert.Equal(400, test.ExpectedStatus);
        Assert.Equal("object", test.ExpectedResponseSchema?.Type);
    }

    [Fact]
    public void Normalize_ValidEdge_IsAcceptedAsAiEdge()
    {
        var operation = GetPets();
        var result = _normalizer.Normalize(
            [Proposal(AiScenarioType.Edge, operation, expectedStatus: 200)],
            [operation],
            []);

        var test = Assert.Single(result.Tests);
        Assert.Equal(GeneratedApiTestKind.AiEdge, test.Kind);
        Assert.Equal(200, test.ExpectedStatus);
    }

    [Fact]
    public void Normalize_UnknownOperation_IsRejected()
    {
        var result = _normalizer.Normalize(
            [Proposal(AiScenarioType.Positive, GetPets(), pathTemplate: "/unknown")],
            [GetPets()],
            []);

        Assert.Empty(result.Tests);
        Assert.Contains(result.Warnings, warning => warning.Contains("Unknown endpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalize_UnsupportedMethod_IsRejected()
    {
        var result = _normalizer.Normalize(
            [Proposal(AiScenarioType.Positive, GetPets(), method: "PATCH")],
            [GetPets()],
            []);

        Assert.Empty(result.Tests);
        Assert.Contains(result.Warnings, warning => warning.Contains("Unsupported HTTP method", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalize_UnknownParameter_IsRejected()
    {
        var operation = GetById();
        var proposal = Proposal(
            AiScenarioType.Positive,
            operation,
            parameters: [new GeneratedApiParameter("nope", "query", false, "1")]);

        var result = _normalizer.Normalize([proposal], [operation], []);

        Assert.Empty(result.Tests);
        Assert.Contains(result.Warnings, warning => warning.Contains("Unknown parameter", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalize_InvalidJsonBody_IsRejected()
    {
        var operation = PostPets();
        var result = _normalizer.Normalize(
            [Proposal(AiScenarioType.Negative, operation, body: "not-json")],
            [operation],
            []);

        Assert.Empty(result.Tests);
        Assert.Contains(result.Warnings, warning => warning.Contains("not valid JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalize_BodyOnOperationWithoutRequestBody_IsRejected()
    {
        var operation = GetPets();
        var result = _normalizer.Normalize(
            [Proposal(AiScenarioType.Positive, operation, body: """{"id":1}""")],
            [operation],
            []);

        Assert.Empty(result.Tests);
        Assert.Contains(result.Warnings, warning => warning.Contains("no request body", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalize_AiExpectedStatus_IsReconciledFromContract()
    {
        var operation = GetPets();
        var result = _normalizer.Normalize(
            [Proposal(AiScenarioType.Positive, operation, expectedStatus: 201)],
            [operation],
            []);

        Assert.Equal(200, Assert.Single(result.Tests).ExpectedStatus);
    }

    [Fact]
    public void Normalize_AiExpectedSchema_IsReconciledFromContract()
    {
        var operation = GetPets();
        var proposal = new AiTestScenario(
            AiScenarioType.Positive,
            operation.Method,
            operation.Path,
            operation.Path,
            [],
            requestBody: null,
            expectedStatus: 200,
            new ImportedSchema("object", [], new Dictionary<string, ImportedSchema>(), [], null),
            "schema from AI",
            "IGNORE",
            "/ignored");

        var result = _normalizer.Normalize([proposal], [operation], []);

        Assert.Equal("array", Assert.Single(result.Tests).ExpectedResponseSchema?.Type);
        Assert.Equal("GET", Assert.Single(result.Tests).SourceMethod);
        Assert.Equal("/pets", Assert.Single(result.Tests).SourcePath);
    }

    [Fact]
    public void Normalize_Positive_PrefersLowestDocumented2xx()
    {
        var operation = new ImportedOperation(
            "GET",
            "/pets",
            [],
            requestBody: null,
            new Dictionary<string, ImportedSchema?>
            {
                ["201"] = new("object", [], new Dictionary<string, ImportedSchema>(), [], null),
                ["204"] = null
            });

        var result = _normalizer.Normalize([Proposal(AiScenarioType.Positive, operation)], [operation], []);

        Assert.Equal(201, Assert.Single(result.Tests).ExpectedStatus);
        Assert.Equal("object", Assert.Single(result.Tests).ExpectedResponseSchema?.Type);
    }

    [Fact]
    public void Normalize_Negative_Prefers400Then422()
    {
        var both = PostPets(responses: new Dictionary<string, ImportedSchema?>
        {
            ["200"] = ObjectSchema(),
            ["422"] = ObjectSchema(),
            ["400"] = new("object", ["error"], new Dictionary<string, ImportedSchema>(), [], null)
        });
        var only422 = PostPets(responses: new Dictionary<string, ImportedSchema?>
        {
            ["200"] = ObjectSchema(),
            ["422"] = ObjectSchema()
        });

        Assert.Equal(400, Assert.Single(_normalizer.Normalize(
            [Proposal(AiScenarioType.Negative, both, body: "{}")], [both], []).Tests).ExpectedStatus);
        Assert.Equal(422, Assert.Single(_normalizer.Normalize(
            [Proposal(AiScenarioType.Negative, only422, body: "{}")], [only422], []).Tests).ExpectedStatus);
    }

    [Fact]
    public void Normalize_Edge_UsesDocumentedStatusOtherwisePositiveFallback()
    {
        var operation = GetPets(responses: new Dictionary<string, ImportedSchema?>
        {
            ["200"] = ArraySchema(),
            ["204"] = null
        });

        var documented = _normalizer.Normalize(
            [Proposal(AiScenarioType.Edge, operation, expectedStatus: 204)],
            [operation],
            []);
        Assert.Equal(204, Assert.Single(documented.Tests).ExpectedStatus);

        var fallback = _normalizer.Normalize(
            [Proposal(AiScenarioType.Edge, operation, expectedStatus: 201)],
            [operation],
            []);
        Assert.Equal(200, Assert.Single(fallback.Tests).ExpectedStatus);
    }

    [Fact]
    public void Normalize_ScenarioKey_IsStableForIdenticalInput()
    {
        var operation = GetPets();
        var first = Assert.Single(_normalizer.Normalize([Proposal(AiScenarioType.Edge, operation)], [operation], []).Tests);
        var second = Assert.Single(_normalizer.Normalize([Proposal(AiScenarioType.Edge, operation)], [operation], []).Tests);

        Assert.Equal(first.ScenarioKey, second.ScenarioKey);
    }

    [Fact]
    public void Normalize_SubstitutedPath_DoesNotAffectScenarioKey()
    {
        var operation = GetById();
        var parameters = new[] { new GeneratedApiParameter("id", "path", true, "1") };
        var first = Proposal(AiScenarioType.Positive, operation, path: "/pets/1", parameters: parameters);
        var second = Proposal(AiScenarioType.Positive, operation, path: "/pets/99", parameters: parameters);

        var result = _normalizer.Normalize([first, second], [operation], []);

        Assert.Equal(Assert.Single(result.Tests).ScenarioKey, _normalizer.Normalize([second], [operation], []).Tests[0].ScenarioKey);
        Assert.Contains(result.Warnings, warning => warning.Contains("Duplicate", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalize_Rationale_DoesNotAffectScenarioKey()
    {
        var operation = GetPets();
        var first = Proposal(AiScenarioType.Positive, operation, rationale: "one");
        var second = Proposal(AiScenarioType.Positive, operation, rationale: "two");

        Assert.Equal(
            Assert.Single(_normalizer.Normalize([first], [operation], []).Tests).ScenarioKey,
            Assert.Single(_normalizer.Normalize([second], [operation], []).Tests).ScenarioKey);
    }

    [Fact]
    public void Normalize_DuplicateAiScenarios_AreRemoved()
    {
        var operation = GetPets();
        var result = _normalizer.Normalize(
            [Proposal(AiScenarioType.Positive, operation), Proposal(AiScenarioType.Positive, operation)],
            [operation],
            []);

        Assert.Single(result.Tests);
        Assert.Contains(result.Warnings, warning => warning.Contains("Duplicate", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalize_DoesNotDuplicateExistingDeterministicTest()
    {
        var operation = GetPets();
        var existing = new GeneratedApiTestCase(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets",
            "/pets",
            [],
            requestBody: null,
            200,
            ArraySchema(),
            "GET",
            "/pets");

        var result = _normalizer.Normalize([Proposal(AiScenarioType.Positive, operation)], [operation], [existing]);

        Assert.Empty(result.Tests);
        Assert.Contains(result.Warnings, warning => warning.Contains("already covers", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalize_EnforcesMaximumSixScenariosPerOperation()
    {
        var operation = PostPets();
        var proposals = new List<AiTestScenario>();
        for (var i = 0; i < 3; i++)
        {
            proposals.Add(Proposal(AiScenarioType.Positive, operation, body: $$"""{"name":"p{{i}}"}"""));
            proposals.Add(Proposal(AiScenarioType.Negative, operation, body: $$"""{"name":"n{{i}}"}"""));
            proposals.Add(Proposal(AiScenarioType.Edge, operation, body: $$"""{"name":"e{{i}}"}"""));
        }

        var result = _normalizer.Normalize(proposals, [operation], []);

        Assert.Equal(6, result.Tests.Count);
        Assert.Equal(2, result.Tests.Count(test => test.Kind == GeneratedApiTestKind.AiPositive));
        Assert.Equal(2, result.Tests.Count(test => test.Kind == GeneratedApiTestKind.AiNegative));
        Assert.Equal(2, result.Tests.Count(test => test.Kind == GeneratedApiTestKind.AiEdge));
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Normalize_InvalidProposals_ProduceWarningsWithoutThrowing()
    {
        var operation = GetPets();
        var result = _normalizer.Normalize(
            [
                Proposal(AiScenarioType.Positive, operation, pathTemplate: "/missing"),
                Proposal(AiScenarioType.Positive, operation, body: "{"),
                new AiTestScenario(AiScenarioType.Positive, "", "", "", [], null, 200, null, null, "", "")
            ],
            [operation],
            []);

        Assert.Empty(result.Tests);
        Assert.Equal(3, result.Warnings.Count);
    }

    private static AiTestScenario Proposal(
        AiScenarioType type,
        ImportedOperation operation,
        string? method = null,
        string? pathTemplate = null,
        string? path = null,
        string? body = null,
        int expectedStatus = 200,
        string? rationale = "Happy path list.",
        IReadOnlyList<GeneratedApiParameter>? parameters = null) =>
        new(
            type,
            method ?? operation.Method,
            pathTemplate ?? operation.Path,
            path ?? operation.Path,
            parameters ?? [],
            body,
            expectedStatus,
            expectedResponseSchema: null,
            rationale,
            "WRONG",
            "/wrong");

    private static ImportedOperation GetPets(IReadOnlyDictionary<string, ImportedSchema?>? responses = null) =>
        new("GET", "/pets", [], requestBody: null, responses ?? new Dictionary<string, ImportedSchema?>
        {
            ["200"] = ArraySchema()
        });

    private static ImportedOperation GetById() =>
        new(
            "GET",
            "/pets/{id}",
            [new ImportedParameter("id", "path", true, new ImportedSchema("integer", [], new Dictionary<string, ImportedSchema>(), [], null))],
            requestBody: null,
            new Dictionary<string, ImportedSchema?> { ["200"] = ObjectSchema() });

    private static ImportedOperation PostPets(IReadOnlyDictionary<string, ImportedSchema?>? responses = null) =>
        new(
            "POST",
            "/pets",
            [],
            ObjectSchema(["name"]),
            responses ?? new Dictionary<string, ImportedSchema?>
            {
                ["201"] = ObjectSchema(),
                ["400"] = ObjectSchema()
            });

    private static ImportedSchema ObjectSchema(IReadOnlyList<string>? required = null) =>
        new("object", required ?? [], new Dictionary<string, ImportedSchema>(), [], null);

    private static ImportedSchema ArraySchema() =>
        new("array", [], new Dictionary<string, ImportedSchema>(), [], ObjectSchema());
}
