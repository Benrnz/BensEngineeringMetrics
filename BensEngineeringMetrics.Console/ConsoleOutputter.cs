using BensEngineeringMetrics.Slack;

namespace BensEngineeringMetrics;

public class ConsoleOutputter : IOutputter
{
    public void WriteLine()
    {
        Console.WriteLine();
    }

    public void Write(string? message)
    {
        Console.Write(SlackStringUtils.RemoveSlackSpecialCharacters(message));
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
        if (message is null)
        {
            return;
        }

        Console.WriteLine(SlackStringUtils.RemoveSlackSpecialCharacters(message));
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
