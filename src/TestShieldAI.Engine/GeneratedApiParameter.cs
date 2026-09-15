namespace TestShieldAI.Engine;

public sealed class GeneratedApiParameter
{
    public GeneratedApiParameter(string name, string location, bool required, string placeholder)
    {
        Name = name;
        Location = location;
        Required = required;
        Placeholder = placeholder;
    }

    public string Name { get; }

    public string Location { get; }

    public bool Required { get; }

    public string Placeholder { get; }
}
