namespace BensEngineeringMetrics;

public class ConsoleOutputter : IOutputter
{
    public void WriteLine(string? message)
    {
        Console.WriteLine(message);
    }

    public void WriteLine(object? someObject)
    {
        if (someObject is null)
        {
            return;
        }

        Console.WriteLine(someObject.ToString());
    }
}
