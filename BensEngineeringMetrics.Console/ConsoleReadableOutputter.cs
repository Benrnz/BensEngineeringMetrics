using System.Text;
using BensEngineeringMetrics.Slack;

namespace BensEngineeringMetrics;

public class ConsoleReadableOutputter : IReadableOutputter
{
    private readonly StringBuilder buffer = new();

    public void WriteLine(string? message)
    {
        Console.WriteLine(SlackStringUtils.RemoveSlackSpecialCharacters(message));
        this.buffer.AppendLine(message);
    }

    public void WriteLine(object? someObject)
    {
        Console.WriteLine(someObject);
        this.buffer.AppendLine(someObject?.ToString());
    }

    public void WriteLine()
    {
        Console.WriteLine();
        this.buffer.AppendLine();
    }

    public void Write(string? message)
    {
        Console.Write(SlackStringUtils.RemoveSlackSpecialCharacters(message));
        this.buffer.Append(message);
    }

    public void Write(object? someObject)
    {
        Console.Write(someObject);
        this.buffer.Append(someObject);
    }

    public string ReadTextAndResetBuffer()
    {
        var result = this.buffer.ToString();
        this.buffer.Clear();
        return result;
    }

    public void ResetBuffer()
    {
        this.buffer.Clear();
    }

    public override string ToString()
    {
        return this.buffer.ToString();
    }
}
