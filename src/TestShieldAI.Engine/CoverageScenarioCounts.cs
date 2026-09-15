namespace TestShieldAI.Engine;

public sealed class CoverageScenarioCounts
{
    public CoverageScenarioCounts(
        int happyPath,
        int negative,
        int aiPositive,
        int aiNegative,
        int aiEdge)
    {
        HappyPath = happyPath;
        Negative = negative;
        AiPositive = aiPositive;
        AiNegative = aiNegative;
        AiEdge = aiEdge;
    }

    public int HappyPath { get; }

    public int Negative { get; }

    public int AiPositive { get; }

    public int AiNegative { get; }

    public int AiEdge { get; }
}
