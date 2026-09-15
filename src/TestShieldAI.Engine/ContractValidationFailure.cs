namespace TestShieldAI.Engine;

public sealed class ContractValidationFailure
{
    public ContractValidationFailure(string code, string jsonPath, string message)
    {
        Code = code;
        JsonPath = jsonPath;
        Message = message;
    }

    public string Code { get; }

    public string JsonPath { get; }

    public string Message { get; }
}
