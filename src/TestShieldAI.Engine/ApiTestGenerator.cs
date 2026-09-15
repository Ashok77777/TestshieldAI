using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TestShieldAI.Engine;

public sealed class ApiTestGenerator : IApiTestGenerator
{
    public const int FallbackExpectedStatus = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public IReadOnlyList<GeneratedApiTestCase> Generate(IReadOnlyList<ImportedOperation> operations)
    {
        if (operations is null || operations.Count == 0)
        {
            return [];
        }

        var cases = new List<GeneratedApiTestCase>();
        foreach (var operation in operations)
        {
            var happyPath = CreateHappyPath(operation);
            cases.Add(happyPath);

            if (ShouldGenerateNegativeBody(operation.RequestBody))
            {
                cases.Add(CreateNegativeMissingRequiredBody(operation, happyPath));
            }
        }

        return cases;
    }

    private static GeneratedApiTestCase CreateHappyPath(ImportedOperation operation)
    {
        var parameters = BuildRequiredParameters(operation);
        var path = SubstitutePathParameters(operation.Path, parameters);
        var (status, schema) = SelectExpectedResponse(operation.Responses);
        var body = BuildHappyPathBody(operation.RequestBody);

        return new GeneratedApiTestCase(
            GeneratedApiTestKind.HappyPath,
            operation.Method,
            operation.Path,
            path,
            parameters,
            body,
            status,
            schema,
            operation.Method,
            operation.Path,
            specKey: operation.SpecKey);
    }

    private static GeneratedApiTestCase CreateNegativeMissingRequiredBody(
        ImportedOperation operation,
        GeneratedApiTestCase happyPath)
    {
        var (status, schema) = SelectNegativeExpectedResponse(operation.Responses);
        var negativeBody = RemoveFirstRequiredProperty(happyPath.RequestBody, operation.RequestBody!);
        return new GeneratedApiTestCase(
            GeneratedApiTestKind.NegativeMissingRequiredBody,
            happyPath.Method,
            happyPath.PathTemplate,
            happyPath.Path,
            happyPath.Parameters,
            negativeBody,
            status,
            schema,
            happyPath.SourceMethod,
            happyPath.SourcePath,
            specKey: happyPath.SpecKey);
    }

    private static (int Status, ImportedSchema? Schema) SelectNegativeExpectedResponse(
        IReadOnlyDictionary<string, ImportedSchema?> responses)
    {
        if (responses.ContainsKey("400") && responses.ContainsKey("422"))
        {
            return (400, responses["400"]);
        }

        if (responses.ContainsKey("400"))
        {
            return (400, responses["400"]);
        }

        if (responses.ContainsKey("422"))
        {
            return (422, responses["422"]);
        }

        responses.TryGetValue("400", out var schema);
        return (400, schema);
    }

    private static bool ShouldGenerateNegativeBody(ImportedSchema? requestBody) =>
        requestBody is not null && requestBody.Required.Count > 0;

    private static IReadOnlyList<GeneratedApiParameter> BuildRequiredParameters(ImportedOperation operation)
    {
        var parameters = new List<GeneratedApiParameter>();
        foreach (var parameter in operation.Parameters)
        {
            if (!parameter.Required)
            {
                continue;
            }

            var location = parameter.Location.ToLowerInvariant();
            if (location is not ("path" or "query" or "header"))
            {
                continue;
            }

            parameters.Add(new GeneratedApiParameter(
                parameter.Name,
                location,
                required: true,
                PlaceholderValue(parameter.Schema)));
        }

        return parameters;
    }

    private static string SubstitutePathParameters(string pathTemplate, IReadOnlyList<GeneratedApiParameter> parameters)
    {
        var path = pathTemplate;
        foreach (var parameter in parameters)
        {
            if (parameter.Location != "path")
            {
                continue;
            }

            path = path.Replace($"{{{parameter.Name}}}", parameter.Placeholder, StringComparison.Ordinal);
        }

        return path;
    }

    private static string PlaceholderValue(ImportedSchema? schema)
    {
        if (schema?.Enum is { Count: > 0 } && !string.IsNullOrEmpty(schema.Enum[0]))
        {
            return schema.Enum[0];
        }

        return NormalizeType(schema?.Type) switch
        {
            "integer" or "int64" => "1",
            "number" => "1",
            "boolean" => "true",
            _ => "a"
        };
    }

    private static string? BuildHappyPathBody(ImportedSchema? requestBody)
    {
        if (requestBody is null)
        {
            return null;
        }

        var node = BuildJsonNode(requestBody);
        return node.ToJsonString(JsonOptions);
    }

    private static string? RemoveFirstRequiredProperty(string? happyPathBody, ImportedSchema requestBody)
    {
        if (happyPathBody is null || requestBody.Required.Count == 0)
        {
            return happyPathBody;
        }

        var node = JsonNode.Parse(happyPathBody);
        if (node is JsonObject obj)
        {
            obj.Remove(requestBody.Required[0]);
            return obj.ToJsonString(JsonOptions);
        }

        return happyPathBody;
    }

    private static JsonNode BuildJsonNode(ImportedSchema? schema)
    {
        if (schema?.Enum is { Count: > 0 })
        {
            return EnumToJsonValue(schema);
        }

        return NormalizeType(schema?.Type) switch
        {
            "integer" or "int64" => JsonValue.Create(1)!,
            "number" => JsonValue.Create(1)!,
            "boolean" => JsonValue.Create(true)!,
            "array" => BuildArray(schema),
            "object" => BuildObject(schema),
            _ when schema?.Properties.Count > 0 || schema?.Required.Count > 0 => BuildObject(schema),
            _ => JsonValue.Create("a")!
        };
    }

    private static JsonArray BuildArray(ImportedSchema? schema)
    {
        if (schema?.Items is null)
        {
            return [];
        }

        return [BuildJsonNode(schema.Items)];
    }

    private static JsonObject BuildObject(ImportedSchema? schema)
    {
        var obj = new JsonObject();
        if (schema is null)
        {
            return obj;
        }

        foreach (var name in schema.Required)
        {
            schema.Properties.TryGetValue(name, out var propertySchema);
            obj[name] = BuildJsonNode(propertySchema);
        }

        return obj;
    }

    private static JsonNode EnumToJsonValue(ImportedSchema schema)
    {
        var first = schema.Enum[0];
        var type = NormalizeType(schema.Type);
        if (type is "integer" or "int64" &&
            int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return JsonValue.Create(integer)!;
        }

        if (type is "number" &&
            double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return JsonValue.Create(number)!;
        }

        if (type is "boolean" && bool.TryParse(first, out var flag))
        {
            return JsonValue.Create(flag)!;
        }

        return JsonValue.Create(first)!;
    }

    private static (int Status, ImportedSchema? Schema) SelectExpectedResponse(
        IReadOnlyDictionary<string, ImportedSchema?> responses)
    {
        var numeric = new List<(int Status, string Key)>();
        foreach (var key in responses.Keys)
        {
            if (key.Length == 3 && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var code))
            {
                numeric.Add((code, key));
            }
        }

        if (numeric.Count == 0)
        {
            // Fallback when the operation has no 3-digit status keys (including default-only).
            responses.TryGetValue("default", out var defaultSchema);
            return (FallbackExpectedStatus, defaultSchema);
        }

        var selected = SelectLowestInRange(numeric, 200, 299)
            ?? SelectLowestInRange(numeric, 300, 399)
            ?? SelectLowestInRange(numeric, 400, 499)
            ?? SelectLowestInRange(numeric, 500, 599)
            ?? numeric.OrderBy(item => item.Status).First();

        return (selected.Status, responses[selected.Key]);
    }

    private static (int Status, string Key)? SelectLowestInRange(
        IReadOnlyList<(int Status, string Key)> statuses,
        int minInclusive,
        int maxInclusive)
    {
        (int Status, string Key)? best = null;
        foreach (var item in statuses)
        {
            if (item.Status < minInclusive || item.Status > maxInclusive)
            {
                continue;
            }

            if (best is null || item.Status < best.Value.Status)
            {
                best = item;
            }
        }

        return best;
    }

    private static string NormalizeType(string? type) =>
        type?.Trim().ToLowerInvariant() ?? "";
}
