namespace TestShieldAI.Engine;

public sealed class ImportedParameter
{
    public ImportedParameter(string name, string location, bool required, ImportedSchema? schema)
    {
        Name = name;
        Location = location;
        Required = required;
        Schema = schema;
    }

    public string Name { get; }

    public string Location { get; }

    public bool Required { get; }

    public ImportedSchema? Schema { get; }
}
