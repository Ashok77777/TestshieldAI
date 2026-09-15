namespace TestShieldAI.Engine;

public interface IApiTestRunner
{
    Task<IReadOnlyList<ApiTestExecutionResult>> RunAsync(
        string baseUrl,
        IReadOnlyList<GeneratedApiTestCase> tests,
        CancellationToken cancellationToken = default);
}
