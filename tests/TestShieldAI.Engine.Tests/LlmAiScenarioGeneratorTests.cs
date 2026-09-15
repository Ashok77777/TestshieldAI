using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class LlmAiScenarioGeneratorTests
{
    [Fact]
    public async Task Generate_ValidJson_ParsesPositiveNegativeAndEdge()
    {
        var operation = PostPets();
        var client = new FakeAiCompletionClient
        {
            Completion = """
                [
                  {
                    "type": "Positive",
                    "method": "POST",
                    "pathTemplate": "/pets",
                    "path": "/pets",
                    "parameters": [],
                    "requestBody": { "name": "Rex" },
                    "expectedStatus": 201,
                    "rationale": "Create a valid pet."
                  },
                  {
                    "type": "Negative",
                    "method": "POST",
                    "pathTemplate": "/pets",
                    "path": "/pets",
                    "parameters": [],
                    "requestBody": { "name": "" },
                    "expectedStatus": 400,
                    "rationale": "Empty name should be rejected."
                  },
                  {
                    "type": "Edge",
                    "method": "POST",
                    "pathTemplate": "/pets",
                    "path": "/pets",
                    "parameters": [],
                    "requestBody": { "name": "A very long pet name for boundary testing" },
                    "expectedStatus": 201,
                    "rationale": "Long name is an edge case."
                  }
                ]
                """
        };
        var generator = new LlmAiScenarioGenerator(client);

        var result = await generator.GenerateAsync([operation], []);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Warnings);
        Assert.Equal(3, result.Scenarios.Count);
        Assert.Equal(AiScenarioType.Positive, result.Scenarios[0].Type);
        Assert.Equal(AiScenarioType.Negative, result.Scenarios[1].Type);
        Assert.Equal(AiScenarioType.Edge, result.Scenarios[2].Type);
        Assert.Equal("Create a valid pet.", result.Scenarios[0].Rationale);
        Assert.Equal("Empty name should be rejected.", result.Scenarios[1].Rationale);
        Assert.Equal("Long name is an edge case.", result.Scenarios[2].Rationale);
        Assert.Contains("Rex", result.Scenarios[0].RequestBody, StringComparison.Ordinal);
        Assert.Equal(201, result.Scenarios[0].ExpectedStatus);
        Assert.Equal(400, result.Scenarios[1].ExpectedStatus);
        Assert.Null(result.Scenarios[0].ExpectedResponseSchema);
    }

    [Fact]
    public async Task Generate_ParsesParametersAndRequestBody()
    {
        var operation = GetById();
        var client = new FakeAiCompletionClient
        {
            Completion = """
                [
                  {
                    "type": "Positive",
                    "method": "GET",
                    "pathTemplate": "/pets/{id}",
                    "path": "/pets/42",
                    "parameters": [
                      { "name": "id", "location": "path", "required": true, "placeholder": "42" }
                    ],
                    "requestBody": null,
                    "expectedStatus": 200,
                    "rationale": "Fetch an existing pet."
                  }
                ]
                """
        };
        var generator = new LlmAiScenarioGenerator(client);

        var result = await generator.GenerateAsync([operation], []);

        var scenario = Assert.Single(result.Scenarios);
        var parameter = Assert.Single(scenario.Parameters);
        Assert.Equal("id", parameter.Name);
        Assert.Equal("path", parameter.Location);
        Assert.True(parameter.Required);
        Assert.Equal("42", parameter.Placeholder);
        Assert.Null(scenario.RequestBody);
        Assert.Equal("/pets/42", scenario.Path);
        Assert.Equal("/pets/{id}", scenario.PathTemplate);
    }

    [Fact]
    public async Task Generate_EmptyArray_SucceedsWithZeroScenarios()
    {
        var client = new FakeAiCompletionClient { Completion = "[]" };
        var generator = new LlmAiScenarioGenerator(client);

        var result = await generator.GenerateAsync([GetPets()], []);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Scenarios);
        Assert.Empty(result.Warnings);
        Assert.Single(client.Prompts);
    }

    [Fact]
    public async Task Generate_MalformedJson_DoesNotThrowAndReturnsWarning()
    {
        var client = new FakeAiCompletionClient { Completion = "{ not json" };
        var generator = new LlmAiScenarioGenerator(client);

        var result = await generator.GenerateAsync([GetPets()], []);

        Assert.False(result.Succeeded);
        Assert.Empty(result.Scenarios);
        Assert.Contains(result.Warnings, warning => warning.Contains("not valid JSON", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generate_MalformedScenario_KeepsValidSibling()
    {
        var client = new FakeAiCompletionClient
        {
            Completion = """
                [
                  {
                    "type": "Positive",
                    "method": "GET",
                    "pathTemplate": "/pets",
                    "path": "/pets",
                    "parameters": [],
                    "expectedStatus": 200,
                    "rationale": "Valid list."
                  },
                  {
                    "type": "UnknownKind",
                    "method": "GET",
                    "pathTemplate": "/pets",
                    "expectedStatus": 200
                  },
                  {
                    "method": "GET",
                    "pathTemplate": "/pets",
                    "expectedStatus": 200
                  },
                  {
                    "type": "Positive",
                    "expectedStatus": 200
                  },
                  {
                    "type": "Positive",
                    "method": "GET",
                    "pathTemplate": "/pets",
                    "parameters": { "id": "1" },
                    "expectedStatus": 200
                  },
                  {
                    "type": "Positive",
                    "method": "GET",
                    "pathTemplate": "/pets",
                    "requestBody": "not-an-object",
                    "expectedStatus": 200
                  }
                ]
                """
        };
        var generator = new LlmAiScenarioGenerator(client);

        var result = await generator.GenerateAsync([GetPets()], []);

        Assert.True(result.Succeeded);
        var kept = Assert.Single(result.Scenarios);
        Assert.Equal("Valid list.", kept.Rationale);
        Assert.Contains(result.Warnings, warning => warning.Contains("unsupported type", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("missing type", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("missing method or pathTemplate", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("invalid parameters structure", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("invalid request body structure", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generate_ClientException_BecomesWarningWithoutThrowing()
    {
        var client = new FakeAiCompletionClient
        {
            Exception = new InvalidOperationException("provider unavailable")
        };
        var generator = new LlmAiScenarioGenerator(client);

        var result = await generator.GenerateAsync([GetPets()], []);

        Assert.False(result.Succeeded);
        Assert.Empty(result.Scenarios);
        Assert.Contains(result.Warnings, warning => warning.Contains("provider unavailable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generate_Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var client = new FakeAiCompletionClient
        {
            Completion = "[]",
            ObserveCancellation = true
        };
        var generator = new LlmAiScenarioGenerator(client);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => generator.GenerateAsync([GetPets()], [], cts.Token));
    }

    [Fact]
    public async Task Generate_PromptContainsContractAndOmitsProjectAndBaseUrl()
    {
        var operation = GetById();
        var existing = new GeneratedApiTestCase(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets/{id}",
            "/pets/1",
            [new GeneratedApiParameter("id", "path", true, "1")],
            requestBody: null,
            expectedStatus: 200,
            expectedResponseSchema: null,
            "GET",
            "/pets/{id}");
        var client = new FakeAiCompletionClient { Completion = "[]" };
        var generator = new LlmAiScenarioGenerator(client);

        await generator.GenerateAsync(
            [operation],
            [existing, UnrelatedProjectShapedTest()]);

        var prompt = Assert.Single(client.Prompts);
        Assert.Contains("Return JSON only", prompt, StringComparison.Ordinal);
        Assert.Contains("GET", prompt, StringComparison.Ordinal);
        Assert.Contains("/pets/{id}", prompt, StringComparison.Ordinal);
        Assert.Contains("\"location\":\"path\"", prompt, StringComparison.Ordinal);
        Assert.Contains("\"schemaType\":\"integer\"", prompt, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"200\"", prompt, StringComparison.Ordinal);
        Assert.Contains("HappyPath", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not decide contract validity, pass/fail, regression, or Safe/Review/Block", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("BaseUrl", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProjectId", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sqlite", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/inventory", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("api-key", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openapi:", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generate_DoesNotCreateScenarioKeyOrNormalize()
    {
        var operation = GetPets();
        var client = new FakeAiCompletionClient
        {
            Completion = """
                [
                  {
                    "type": "Positive",
                    "method": "GET",
                    "pathTemplate": "/pets",
                    "path": "/pets",
                    "parameters": [
                      { "name": "unknown", "location": "query", "placeholder": "1" }
                    ],
                    "expectedStatus": 999,
                    "rationale": "Hallucinated status and parameter."
                  },
                  {
                    "type": "Positive",
                    "method": "GET",
                    "pathTemplate": "/pets",
                    "path": "/pets",
                    "parameters": [
                      { "name": "unknown", "location": "query", "placeholder": "1" }
                    ],
                    "expectedStatus": 999,
                    "rationale": "Duplicate of the previous scenario."
                  }
                ]
                """
        };
        var generator = new LlmAiScenarioGenerator(client);

        var result = await generator.GenerateAsync([operation], []);

        Assert.Equal(2, result.Scenarios.Count);
        Assert.All(result.Scenarios, scenario =>
        {
            Assert.Equal(999, scenario.ExpectedStatus);
            Assert.Null(scenario.ExpectedResponseSchema);
            Assert.Equal("unknown", Assert.Single(scenario.Parameters).Name);
        });
        Assert.Equal("Hallucinated status and parameter.", result.Scenarios[0].Rationale);
    }

    [Fact]
    public async Task Generate_MultipleOperations_CallsClientPerOperation()
    {
        var client = new FakeAiCompletionClient
        {
            CompletionFactory = prompt => prompt.Contains("/pets/{id}", StringComparison.Ordinal)
                ? """
                  [{ "type": "Positive", "method": "GET", "pathTemplate": "/pets/{id}", "path": "/pets/1", "expectedStatus": 200, "rationale": "By id." }]
                  """
                : """
                  [{ "type": "Positive", "method": "GET", "pathTemplate": "/pets", "path": "/pets", "expectedStatus": 200, "rationale": "List." }]
                  """
        };
        var generator = new LlmAiScenarioGenerator(client);

        var result = await generator.GenerateAsync([GetPets(), GetById()], []);

        Assert.Equal(2, client.Prompts.Count);
        Assert.Equal(2, result.Scenarios.Count);
        Assert.Equal("/pets", result.Scenarios[0].PathTemplate);
        Assert.Equal("/pets/{id}", result.Scenarios[1].PathTemplate);
        Assert.Equal("GET", result.Scenarios[0].SourceMethod);
        Assert.Equal("/pets", result.Scenarios[0].SourcePath);
        Assert.Equal("/pets/{id}", result.Scenarios[1].SourcePath);
    }

    [Fact]
    public async Task Generate_RespectsMaxScenariosPerOperation()
    {
        var items = Enumerable.Range(0, 8).Select(i => $$"""
            {
              "type": "Positive",
              "method": "POST",
              "pathTemplate": "/pets",
              "path": "/pets",
              "requestBody": { "name": "pet-{{i}}" },
              "expectedStatus": 201,
              "rationale": "Scenario {{i}}."
            }
            """);
        var client = new FakeAiCompletionClient
        {
            Completion = "[" + string.Join(",", items) + "]"
        };
        var generator = new LlmAiScenarioGenerator(
            client,
            new AiScenarioGeneratorOptions { MaxScenariosPerOperation = 3 });

        var result = await generator.GenerateAsync([PostPets()], []);

        Assert.Equal(3, result.Scenarios.Count);
        Assert.Contains(result.Warnings, warning => warning.Contains("Truncated", StringComparison.Ordinal));
        Assert.Contains("pet-0", result.Scenarios[0].RequestBody, StringComparison.Ordinal);
        Assert.Contains("pet-2", result.Scenarios[2].RequestBody, StringComparison.Ordinal);
        Assert.Contains("at most 3", client.Prompts[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_ClientExceptionOnOneOperation_DoesNotStopOthers()
    {
        var client = new FakeAiCompletionClient
        {
            CompletionFactory = prompt =>
            {
                if (prompt.Contains("/pets/{id}", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("timeout");
                }

                return """
                    [{ "type": "Edge", "method": "GET", "pathTemplate": "/pets", "path": "/pets", "expectedStatus": 200, "rationale": "Empty list." }]
                    """;
            }
        };
        var generator = new LlmAiScenarioGenerator(client);

        var result = await generator.GenerateAsync([GetPets(), GetById()], []);

        var scenario = Assert.Single(result.Scenarios);
        Assert.Equal(AiScenarioType.Edge, scenario.Type);
        Assert.Equal("Empty list.", scenario.Rationale);
        Assert.Contains(result.Warnings, warning => warning.Contains("timeout", StringComparison.Ordinal));
        Assert.True(result.Succeeded);
    }

    private static ImportedOperation GetPets() =>
        new("GET", "/pets", [], requestBody: null, new Dictionary<string, ImportedSchema?>
        {
            ["200"] = new ImportedSchema("array", [], new Dictionary<string, ImportedSchema>(), [], null)
        });

    private static ImportedOperation GetById() =>
        new(
            "GET",
            "/pets/{id}",
            [
                new ImportedParameter(
                    "id",
                    "path",
                    true,
                    new ImportedSchema("integer", [], new Dictionary<string, ImportedSchema>(), ["1", "2"], null))
            ],
            requestBody: null,
            new Dictionary<string, ImportedSchema?>
            {
                ["200"] = new ImportedSchema("object", [], new Dictionary<string, ImportedSchema>(), [], null)
            });

    private static ImportedOperation PostPets() =>
        new(
            "POST",
            "/pets",
            [],
            new ImportedSchema(
                "object",
                ["name"],
                new Dictionary<string, ImportedSchema>
                {
                    ["name"] = new ImportedSchema("string", [], new Dictionary<string, ImportedSchema>(), [], null)
                },
                [],
                null),
            new Dictionary<string, ImportedSchema?>
            {
                ["201"] = new ImportedSchema("object", [], new Dictionary<string, ImportedSchema>(), [], null),
                ["400"] = new ImportedSchema("object", [], new Dictionary<string, ImportedSchema>(), [], null)
            });

    private static GeneratedApiTestCase UnrelatedProjectShapedTest() =>
        new(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/inventory",
            "/inventory",
            [],
            requestBody: null,
            expectedStatus: 200,
            expectedResponseSchema: null,
            "GET",
            "/inventory");

    private sealed class FakeAiCompletionClient : IAiCompletionClient
    {
        public List<string> Prompts { get; } = [];

        public string Completion { get; init; } = "[]";

        public Func<string, string>? CompletionFactory { get; init; }

        public Exception? Exception { get; init; }

        public bool ObserveCancellation { get; init; }

        public Task<string> CompleteJsonAsync(string prompt, CancellationToken cancellationToken = default)
        {
            Prompts.Add(prompt);
            if (ObserveCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (Exception is not null)
            {
                throw Exception;
            }

            var json = CompletionFactory is null ? Completion : CompletionFactory(prompt);
            return Task.FromResult(json);
        }
    }
}
