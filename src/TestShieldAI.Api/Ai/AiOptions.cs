namespace TestShieldAI.Api.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public bool Enabled { get; set; }

    public AiProvider Provider { get; set; } = AiProvider.None;

    public string Endpoint { get; set; } = "";

    public string ModelOrDeployment { get; set; } = "";

    public int MaxScenariosPerOperation { get; set; } = 6;

    public int TimeoutSeconds { get; set; } = 30;
}
