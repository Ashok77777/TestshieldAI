namespace TestShieldAI.Engine;

public interface IRegressionDetector
{
    RegressionDetectionResult Detect(
        IReadOnlyList<RegressionTestSnapshot> baseline,
        IReadOnlyList<RegressionTestSnapshot> current);
}
