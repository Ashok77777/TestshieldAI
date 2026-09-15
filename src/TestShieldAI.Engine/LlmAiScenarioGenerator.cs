using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TestShieldAI.Engine;

public sealed class LlmAiScenarioGenerator : IAiScenarioGenerator
{
    private readonly IAiCompletionClient _completionClient;
    private readonly int _maxScenariosPerOperation;

    public LlmAiScenarioGenerator(
        IAiCompletionClient completionClient,
        AiScenarioGeneratorOptions? options = null)
    {
        _completionClient = completionClient ?? throw new ArgumentNullException(nameof(completionClient));
        var configured = options?.MaxScenariosPerOperation
            ?? AiScenarioGeneratorOptions.DefaultMaxScenariosPerOperation;
        _maxScenariosPerOperation = configured > 0
            ? configured
            : AiScenarioGeneratorOptions.DefaultMaxScenariosPerOperation;
    }

    public async Task<AiScenarioGenerationResult> GenerateAsync(
        IReadOnlyList<ImportedOperation> operations,
        IReadOnlyList<GeneratedApiTestCase> existingTests,
        CancellationToken cancellationToken = default)
    {
        operations ??= [];
        existingTests ??= [];

        var scenarios = new List<AiTestScenario>();
        var warnings = new List<string>();
        var hadHardFailure = false;

        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation is null)
            {
                continue;
            }

            var prompt = BuildPrompt(operation, existingTests, _maxScenariosPerOperation);
            string completion;
            try
            {
                completion = await _completionClient
                    .CompleteJsonAsync(prompt, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                warnings.Add($"AI completion for {Label(operation)} failed: {ex.Message}");
                hadHardFailure = true;
                continue;
            }

            if (!TryReadScenarioArray(completion, operation, warnings, out var parsed))
            {
                hadHardFailure = true;
                continue;
            }

            if (parsed.Count > _maxScenariosPerOperation)
            {
                warnings.Add(
                    $"Truncated AI scenarios for {Label(operation)} to {_maxScenariosPerOperation}.");
                parsed.RemoveRange(
                    _maxScenariosPerOperation,
                    parsed.Count - _maxScenariosPerOperation);
            }

            scenarios.AddRange(parsed);
        }

        var succeeded = scenarios.Count > 0 || !hadHardFailure;
        return new AiScenarioGenerationResult(scenarios, warnings, succeeded);
    }

    private static string BuildPrompt(
        ImportedOperation operation,
        IReadOnlyList<GeneratedApiTestCase> existingTests,
        int maxScenarios)
    {
        var contract = new JsonObject
        {
            ["method"] = operation.Method,
            ["path"] = operation.Path,
            ["parameters"] = ParametersJson(operation.Parameters),
            ["requestBodySchema"] = SchemaJson(operation.RequestBody),
            ["responses"] = ResponsesJson(operation.Responses),
            ["existingDeterministicTests"] = ExistingTestsJson(operation, existingTests)
        };

        return $$"""
            Generate extra API test scenarios for exactly one operation.
            Return JSON only. Do not wrap the response in markdown. Do not include commentary.

            Return a JSON array of at most {{maxScenarios.ToString(CultureInfo.InvariantCulture)}} objects with this exact shape:
            [
              {
                "type": "Positive|Negative|Edge",
                "method": "...",
                "pathTemplate": "...",
                "path": "...",
                "parameters": [
                  { "name": "...", "location": "path|query|header", "required": false, "placeholder": "..." }
                ],
                "requestBody": {},
                "expectedStatus": 200,
                "rationale": "..."
              }
            ]

            Rules:
            - Use only the contract operation below. Do not invent endpoints, HTTP methods, or parameter names.
            - type must be Positive, Negative, or Edge.
            - method and pathTemplate must match the contract operation.
            - path may substitute path-parameter placeholders.
            - parameters must be a JSON array of objects. requestBody must be a JSON object, or null/omitted when there is no body.
            - expectedStatus is a proposed HTTP status only. TestShield AI reconciles status and response schema from the contract.
            - rationale is a short explanation of the scenario.
            - Do not decide contract validity, pass/fail, regression, or Safe/Review/Block.
            - Do not include project names, base URLs, API keys, database identifiers, raw OpenAPI text, live response bodies, or baseline data.

            Operation contract:
            {{contract.ToJsonString()}}
            """;
    }

    private static JsonArray ParametersJson(IReadOnlyList<ImportedParameter> parameters)
    {
        var array = new JsonArray();
        foreach (var parameter in parameters ?? [])
        {
            if (parameter is null)
            {
                continue;
            }

            array.Add(new JsonObject
            {
                ["name"] = parameter.Name,
                ["location"] = parameter.Location,
                ["required"] = parameter.Required,
                ["schemaType"] = parameter.Schema?.Type,
                ["enum"] = EnumJson(parameter.Schema)
            });
        }

        return array;
    }

    private static JsonArray EnumJson(ImportedSchema? schema)
    {
        var array = new JsonArray();
        foreach (var value in schema?.Enum ?? [])
        {
            array.Add(value);
        }

        return array;
    }

    private static JsonArray ResponsesJson(IReadOnlyDictionary<string, ImportedSchema?> responses)
    {
        var array = new JsonArray();
        foreach (var response in responses ?? new Dictionary<string, ImportedSchema?>())
        {
            array.Add(new JsonObject
            {
                ["status"] = response.Key,
                ["schema"] = SchemaJson(response.Value)
            });
        }

        return array;
    }

    private static JsonArray ExistingTestsJson(
        ImportedOperation operation,
        IReadOnlyList<GeneratedApiTestCase> existingTests)
    {
        var array = new JsonArray();
        foreach (var test in existingTests)
        {
            if (test is null || !MatchesOperation(test, operation))
            {
                continue;
            }

            var parameters = new JsonArray();
            foreach (var parameter in test.Parameters ?? [])
            {
                parameters.Add(new JsonObject
                {
                    ["name"] = parameter.Name,
                    ["location"] = parameter.Location,
                    ["required"] = parameter.Required,
                    ["placeholder"] = parameter.Placeholder
                });
            }

            JsonNode? body = null;
            if (!string.IsNullOrWhiteSpace(test.RequestBody))
            {
                try
                {
                    body = JsonNode.Parse(test.RequestBody);
                }
                catch (JsonException)
                {
                    body = test.RequestBody;
                }
            }

            array.Add(new JsonObject
            {
                ["kind"] = test.Kind.ToString(),
                ["method"] = test.Method,
                ["pathTemplate"] = test.PathTemplate,
                ["path"] = test.Path,
                ["parameters"] = parameters,
                ["requestBody"] = body,
                ["expectedStatus"] = test.ExpectedStatus
            });
        }

        return array;
    }

    private static bool MatchesOperation(GeneratedApiTestCase test, ImportedOperation operation) =>
        string.Equals(test.SourceMethod, operation.Method, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(test.SourcePath, operation.Path, StringComparison.Ordinal);

    private static JsonNode? SchemaJson(ImportedSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        var obj = new JsonObject
        {
            ["type"] = schema.Type
        };

        if (schema.Required.Count > 0)
        {
            var required = new JsonArray();
            foreach (var name in schema.Required)
            {
                required.Add(name);
            }

            obj["required"] = required;
        }

        if (schema.Enum.Count > 0)
        {
            obj["enum"] = EnumJson(schema);
        }

        if (schema.Properties.Count > 0)
        {
            var properties = new JsonObject();
            foreach (var property in schema.Properties)
            {
                properties[property.Key] = SchemaJson(property.Value);
            }

            obj["properties"] = properties;
        }

        if (schema.Items is not null)
        {
            obj["items"] = SchemaJson(schema.Items);
        }

        return obj;
    }

    private static bool TryReadScenarioArray(
        string? completion,
        ImportedOperation operation,
        List<string> warnings,
        out List<AiTestScenario> scenarios)
    {
        scenarios = [];
        var text = NormalizeCompletion(completion);
        if (string.IsNullOrWhiteSpace(text))
        {
            warnings.Add($"AI completion for {Label(operation)} was not valid JSON.");
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            warnings.Add($"AI completion for {Label(operation)} was not valid JSON.");
            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                warnings.Add($"AI completion for {Label(operation)} was not valid JSON.");
                return false;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (!TryReadScenario(element, operation, warnings, out var scenario))
                {
                    continue;
                }

                scenarios.Add(scenario);
            }
        }

        return true;
    }

    private static bool TryReadScenario(
        JsonElement element,
        ImportedOperation operation,
        List<string> warnings,
        out AiTestScenario scenario)
    {
        scenario = null!;
        if (element.ValueKind != JsonValueKind.Object)
        {
            warnings.Add($"Rejected scenario for {Label(operation)}: invalid parameters/body structure.");
            return false;
        }

        if (!TryGetString(element, "type", out var typeText) || string.IsNullOrWhiteSpace(typeText))
        {
            warnings.Add($"Rejected scenario for {Label(operation)}: missing type.");
            return false;
        }

        if (!TryParseType(typeText, out var type))
        {
            warnings.Add($"Rejected scenario for {Label(operation)}: unsupported type '{typeText}'.");
            return false;
        }

        TryGetString(element, "method", out var method);
        TryGetString(element, "pathTemplate", out var pathTemplate);
        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(pathTemplate))
        {
            warnings.Add($"Rejected scenario for {Label(operation)}: missing method or pathTemplate.");
            return false;
        }

        if (!TryReadParameters(element, operation, warnings, out var parameters))
        {
            return false;
        }

        if (!TryReadRequestBody(element, operation, warnings, out var requestBody))
        {
            return false;
        }

        if (!TryReadExpectedStatus(element, out var expectedStatus))
        {
            warnings.Add($"Rejected scenario for {Label(operation)}: invalid parameters/body structure.");
            return false;
        }

        TryGetString(element, "path", out var path);
        TryGetString(element, "rationale", out var rationale);

        scenario = new AiTestScenario(
            type,
            method.Trim(),
            pathTemplate.Trim(),
            string.IsNullOrWhiteSpace(path) ? pathTemplate.Trim() : path.Trim(),
            parameters,
            requestBody,
            expectedStatus,
            expectedResponseSchema: null,
            string.IsNullOrWhiteSpace(rationale) ? null : rationale.Trim(),
            operation.Method,
            operation.Path);
        return true;
    }

    private static bool TryReadParameters(
        JsonElement element,
        ImportedOperation operation,
        List<string> warnings,
        out IReadOnlyList<GeneratedApiParameter> parameters)
    {
        parameters = [];
        if (!TryGetProperty(element, "parameters", out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            warnings.Add($"Rejected scenario for {Label(operation)}: invalid parameters structure.");
            return false;
        }

        var parsed = new List<GeneratedApiParameter>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                warnings.Add($"Rejected scenario for {Label(operation)}: invalid parameters structure.");
                return false;
            }

            if (!TryGetString(item, "name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                warnings.Add($"Rejected scenario for {Label(operation)}: invalid parameters structure.");
                return false;
            }

            TryGetString(item, "location", out var location);
            TryGetString(item, "placeholder", out var placeholder);
            if (string.IsNullOrWhiteSpace(placeholder))
            {
                TryGetString(item, "value", out placeholder);
            }

            var required = false;
            if (TryGetProperty(item, "required", out var requiredElement) &&
                requiredElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                required = requiredElement.GetBoolean();
            }

            parsed.Add(new GeneratedApiParameter(
                name.Trim(),
                location?.Trim() ?? "",
                required,
                placeholder?.Trim() ?? ""));
        }

        parameters = parsed;
        return true;
    }

    private static bool TryReadRequestBody(
        JsonElement element,
        ImportedOperation operation,
        List<string> warnings,
        out string? requestBody)
    {
        requestBody = null;
        if (!TryGetProperty(element, "requestBody", out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            warnings.Add($"Rejected scenario for {Label(operation)}: invalid request body structure.");
            return false;
        }

        requestBody = value.GetRawText();
        return true;
    }

    private static bool TryReadExpectedStatus(JsonElement element, out int expectedStatus)
    {
        expectedStatus = 0;
        if (!TryGetProperty(element, "expectedStatus", out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out expectedStatus))
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out expectedStatus))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseType(string typeText, out AiScenarioType type)
    {
        if (Enum.TryParse(typeText.Trim(), ignoreCase: true, out type) &&
            Enum.IsDefined(type))
        {
            return true;
        }

        type = default;
        return false;
    }

    private static string NormalizeCompletion(string? completion)
    {
        if (string.IsNullOrWhiteSpace(completion))
        {
            return "";
        }

        var text = completion.Trim().Trim('\uFEFF');
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewline = text.IndexOf('\n');
        if (firstNewline >= 0)
        {
            text = text[(firstNewline + 1)..];
        }

        var fence = text.LastIndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            text = text[..fence];
        }

        return text.Trim();
    }

    private static bool TryGetString(JsonElement element, string name, out string? value)
    {
        value = null;
        if (!TryGetProperty(element, name, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        return false;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string Label(ImportedOperation operation) => $"{operation.Method} {operation.Path}";
}
