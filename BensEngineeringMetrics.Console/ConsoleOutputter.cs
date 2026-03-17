namespace BensEngineeringMetrics;

public class ConsoleOutputter : IOutputter
{
    public void WriteLine()
    {
        Console.WriteLine();
    }

    public void Write(string? message)
    {
        Console.Write(message);
    }

    public void Write(object? someObject)
    {
        if (someObject is null)
        {
            return;
        }

        Console.Write(someObject.ToString());
    }

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
