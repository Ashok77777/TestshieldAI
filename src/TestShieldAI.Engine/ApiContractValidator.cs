using System.Globalization;
using System.Text.Json;

namespace TestShieldAI.Engine;

public sealed class ApiContractValidator : IApiContractValidator
{
    public const string StatusMismatch = "STATUS_MISMATCH";
    public const string BodyNotJson = "BODY_NOT_JSON";
    public const string SchemaRequired = "SCHEMA_REQUIRED";
    public const string SchemaType = "SCHEMA_TYPE";
    public const string SchemaEnum = "SCHEMA_ENUM";
    public const string SchemaEmpty = "SCHEMA_EMPTY";
    public const string UnexpectedBody = "UNEXPECTED_BODY";
    public const string ExecutionError = "EXECUTION_ERROR";

    public ContractValidationResult Validate(GeneratedApiTestCase test, ApiTestExecutionResult execution)
    {
        if (!string.IsNullOrWhiteSpace(execution.Error))
        {
            return new ContractValidationResult(
                ContractValidationOutcome.Error,
                statusValid: false,
                schemaValid: null,
                [
                    new ContractValidationFailure(
                        ExecutionError,
                        "$",
                        execution.Error)
                ]);
        }

        var statusValid = execution.ActualStatus is int actual && actual == test.ExpectedStatus;
        if (!statusValid)
        {
            var actualText = execution.ActualStatus?.ToString(CultureInfo.InvariantCulture) ?? "none";
            return new ContractValidationResult(
                ContractValidationOutcome.Failed,
                statusValid: false,
                schemaValid: null,
                [
                    new ContractValidationFailure(
                        StatusMismatch,
                        "$",
                        $"Expected status {test.ExpectedStatus}, actual {actualText}.")
                ]);
        }

        if (test.ExpectedResponseSchema is null)
        {
            if (test.ExpectedStatus == 204 && HasBody(execution.Body))
            {
                return Failed(
                    statusValid: true,
                    schemaValid: false,
                    new ContractValidationFailure(
                        UnexpectedBody,
                        "$",
                        "A 204 response must not include a body."));
            }

            return new ContractValidationResult(
                ContractValidationOutcome.Passed,
                statusValid: true,
                schemaValid: null,
                []);
        }

        var schemaFailures = ValidateBody(test.ExpectedResponseSchema, execution.Body);
        if (schemaFailures.Count > 0)
        {
            return new ContractValidationResult(
                ContractValidationOutcome.Failed,
                statusValid: true,
                schemaValid: false,
                schemaFailures);
        }

        return new ContractValidationResult(
            ContractValidationOutcome.Passed,
            statusValid: true,
            schemaValid: true,
            []);
    }

    private static IReadOnlyList<ContractValidationFailure> ValidateBody(ImportedSchema schema, string? body)
    {
        if (!HasBody(body))
        {
            return
            [
                new ContractValidationFailure(
                    SchemaEmpty,
                    "$",
                    "Response body was empty but a JSON schema was expected.")
            ];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body!);
        }
        catch (JsonException ex)
        {
            return
            [
                new ContractValidationFailure(
                    BodyNotJson,
                    "$",
                    $"Response body is not valid JSON: {ex.Message}")
            ];
        }

        using (document)
        {
            var failures = new List<ContractValidationFailure>();
            ValidateValue(schema, document.RootElement, "$", failures);
            return failures;
        }
    }

    private static void ValidateValue(
        ImportedSchema schema,
        JsonElement value,
        string jsonPath,
        List<ContractValidationFailure> failures)
    {
        if (schema.Enum.Count > 0 && !EnumMatches(schema, value))
        {
            failures.Add(new ContractValidationFailure(
                SchemaEnum,
                jsonPath,
                $"Value '{FormatValue(value)}' is not one of the allowed enum values."));
            return;
        }

        var type = NormalizeType(schema);
        if (type is "object" || schema.Properties.Count > 0 || schema.Required.Count > 0)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                failures.Add(TypeFailure(jsonPath, "object", value.ValueKind));
                return;
            }

            ValidateObject(schema, value, jsonPath, failures);
            return;
        }

        if (type is "array" || schema.Items is not null)
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                failures.Add(TypeFailure(jsonPath, "array", value.ValueKind));
                return;
            }

            ValidateArray(schema, value, jsonPath, failures);
            return;
        }

        if (type is "string" && value.ValueKind != JsonValueKind.String)
        {
            failures.Add(TypeFailure(jsonPath, "string", value.ValueKind));
            return;
        }

        if (type is "integer" or "int64" && !IsInteger(value))
        {
            failures.Add(TypeFailure(jsonPath, "integer", value.ValueKind));
            return;
        }

        if (type is "number" && value.ValueKind != JsonValueKind.Number)
        {
            failures.Add(TypeFailure(jsonPath, "number", value.ValueKind));
            return;
        }

        if (type is "boolean" && value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            failures.Add(TypeFailure(jsonPath, "boolean", value.ValueKind));
        }
    }

    private static void ValidateObject(
        ImportedSchema schema,
        JsonElement obj,
        string jsonPath,
        List<ContractValidationFailure> failures)
    {
        foreach (var required in schema.Required)
        {
            if (!obj.TryGetProperty(required, out _))
            {
                failures.Add(new ContractValidationFailure(
                    SchemaRequired,
                    ChildPath(jsonPath, required),
                    $"Required property '{required}' is missing."));
            }
        }

        foreach (var property in schema.Properties)
        {
            if (!obj.TryGetProperty(property.Key, out var child))
            {
                continue;
            }

            ValidateValue(property.Value, child, ChildPath(jsonPath, property.Key), failures);
        }
    }

    private static void ValidateArray(
        ImportedSchema schema,
        JsonElement array,
        string jsonPath,
        List<ContractValidationFailure> failures)
    {
        if (schema.Items is null)
        {
            return;
        }

        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            ValidateValue(schema.Items, item, $"{jsonPath}[{index}]", failures);
            index++;
        }
    }

    private static bool EnumMatches(ImportedSchema schema, JsonElement value)
    {
        var text = FormatValue(value);
        return schema.Enum.Any(candidate => string.Equals(candidate, text, StringComparison.Ordinal));
    }

    private static bool IsInteger(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        return value.TryGetInt64(out _);
    }

    private static string FormatValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => value.GetRawText()
    };

    private static ContractValidationFailure TypeFailure(string jsonPath, string expected, JsonValueKind actual) =>
        new(
            SchemaType,
            jsonPath,
            $"Expected type '{expected}', actual '{actual}'.");

    private static string ChildPath(string jsonPath, string name) =>
        jsonPath == "$" ? $"$.{name}" : $"{jsonPath}.{name}";

    private static bool HasBody(string? body) => !string.IsNullOrWhiteSpace(body);

    private static string NormalizeType(ImportedSchema schema) =>
        schema.Type?.Trim().ToLowerInvariant() ?? "";

    private static ContractValidationResult Failed(
        bool statusValid,
        bool? schemaValid,
        ContractValidationFailure failure) =>
        new(ContractValidationOutcome.Failed, statusValid, schemaValid, [failure]);
}
