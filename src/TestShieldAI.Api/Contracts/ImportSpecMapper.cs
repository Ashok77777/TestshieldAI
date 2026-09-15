using TestShieldAI.Engine;

namespace TestShieldAI.Api.Contracts;

public static class ImportSpecMapper
{
    public static ImportSpecResponse ToResponse(OpenApiImportResult result)
    {
        var operations = result.Operations.Select(ToDto).ToList();
        return new ImportSpecResponse { Operations = operations };
    }

    private static ImportedOperationDto ToDto(ImportedOperation operation) =>
        new()
        {
            Method = operation.Method,
            Path = operation.Path,
            Parameters = operation.Parameters.Select(ToDto).ToList(),
            RequestBody = ToDto(operation.RequestBody),
            Responses = operation.Responses.ToDictionary(
                pair => pair.Key,
                pair => ToDto(pair.Value),
                StringComparer.Ordinal)
        };

    private static ImportedParameterDto ToDto(ImportedParameter parameter) =>
        new()
        {
            Name = parameter.Name,
            Location = parameter.Location,
            Required = parameter.Required,
            Schema = ToDto(parameter.Schema)
        };

    public static ImportedSchemaDto? ToDto(ImportedSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        return new ImportedSchemaDto
        {
            Type = schema.Type,
            Required = schema.Required,
            Properties = schema.Properties.ToDictionary(
                pair => pair.Key,
                pair => ToDto(pair.Value)!,
                StringComparer.Ordinal),
            Enum = schema.Enum,
            Items = ToDto(schema.Items)
        };
    }
}
