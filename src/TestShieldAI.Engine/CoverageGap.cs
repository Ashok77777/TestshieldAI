namespace TestShieldAI.Engine;

public sealed class CoverageGap
{
    public CoverageGap(string specKey, string method, string path, string reason)
    {
        SpecKey = specKey;
        Method = method;
        Path = path;
        Reason = reason;
    }

    public string SpecKey { get; }

    public string Method { get; }

    public string Path { get; }

    public string Reason { get; }
}
