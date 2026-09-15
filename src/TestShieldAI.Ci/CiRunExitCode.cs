namespace TestShieldAI.Ci;

public static class CiRunExitCode
{
    public const int Success = 0;

    public const int Failure = 1;

    public static int ForHttpStatus(int statusCode) =>
        statusCode is >= 200 and < 300 ? Success : Failure;

    public static int ForDecision(string? decision)
    {
        if (string.Equals(decision, "Safe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "Review", StringComparison.OrdinalIgnoreCase))
        {
            return Success;
        }

        return Failure;
    }

    public static int ForRun(int httpStatus, string? decision)
    {
        if (ForHttpStatus(httpStatus) == Failure)
        {
            return Failure;
        }

        return ForDecision(decision);
    }
}
