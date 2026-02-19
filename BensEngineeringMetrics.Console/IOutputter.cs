namespace BensEngineeringMetrics;

public interface IOutputter
{
    void WriteLine(string? message);
    void WriteLine(object someObject);
    void WriteLine();
}
