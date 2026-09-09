namespace TestShieldAI.Engine;

public sealed class ImportedSchema
{
    public ImportedSchema(
        string? type,
        IReadOnlyList<string> required,
        IReadOnlyDictionary<string, ImportedSchema> properties,
        IReadOnlyList<string> @enum,
        ImportedSchema? items)
    {
        Type = type;
        Required = required;
        Properties = properties;
        Enum = @enum;
        Items = items;
    }

    public string? Type { get; }

    public IReadOnlyList<string> Required { get; }

    public IReadOnlyDictionary<string, ImportedSchema> Properties { get; }

    public IReadOnlyList<string> Enum { get; }

    public ImportedSchema? Items { get; }
}
