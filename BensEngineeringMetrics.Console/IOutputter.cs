namespace BensEngineeringMetrics;

public interface IOutputter
{
    void Write(string? message);
    void Write(object? someObject);
    void WriteLine(string? message);
    void WriteLine(object? someObject);
    void WriteLine();
}
