namespace TestShieldAI.Engine;

public interface IApiContractValidator
{
    ContractValidationResult Validate(GeneratedApiTestCase test, ApiTestExecutionResult execution);
}
