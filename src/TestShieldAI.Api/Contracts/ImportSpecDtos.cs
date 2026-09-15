namespace TestShieldAI.Api.Contracts;

public sealed class ImportSpecRequest
{
    public string? Specification { get; set; }
}

public sealed class ImportSpecResponse
{
    public required IReadOnlyList<ImportedOperationDto> Operations { get; init; }
}

public sealed class ImportedOperationDto
{
    public required string Method { get; init; }

    public required string Path { get; init; }

    public required IReadOnlyList<ImportedParameterDto> Parameters { get; init; }

    public ImportedSchemaDto? RequestBody { get; init; }

    public required IReadOnlyDictionary<string, ImportedSchemaDto?> Responses { get; init; }
}

public sealed class ImportedParameterDto
{
    public required string Name { get; init; }

    public required string Location { get; init; }

    public required bool Required { get; init; }

    public ImportedSchemaDto? Schema { get; init; }
}

public sealed class ImportedSchemaDto
{
    public string? Type { get; init; }

    public required IReadOnlyList<string> Required { get; init; }

    public required IReadOnlyDictionary<string, ImportedSchemaDto> Properties { get; init; }

    public required IReadOnlyList<string> Enum { get; init; }

    public ImportedSchemaDto? Items { get; init; }
}
