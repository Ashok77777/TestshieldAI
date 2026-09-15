namespace TestShieldAI.Ci;

public static class CiSecretRedactor
{
    public static string Redact(string? text, string? secret)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(secret))
        {
            return text ?? "";
        }

        return text.Replace(secret, "***", StringComparison.Ordinal);
    }
}
