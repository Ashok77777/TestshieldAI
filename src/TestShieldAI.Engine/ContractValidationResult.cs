namespace TestShieldAI.Engine;

public sealed class ContractValidationResult
{
    public ContractValidationResult(
        ContractValidationOutcome outcome,
        bool statusValid,
        bool? schemaValid,
        IReadOnlyList<ContractValidationFailure> failures)
    {
        Outcome = outcome;
        StatusValid = statusValid;
        SchemaValid = schemaValid;
        Failures = failures;
    }

    public ContractValidationOutcome Outcome { get; }

    public bool StatusValid { get; }

    public bool? SchemaValid { get; }

    public IReadOnlyList<ContractValidationFailure> Failures { get; }
}
