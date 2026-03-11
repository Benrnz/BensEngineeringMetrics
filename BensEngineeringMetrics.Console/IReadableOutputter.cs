namespace BensEngineeringMetrics;

public interface IReadableOutputter : IOutputter
{
    string ReadTextAndResetBuffer();
}
