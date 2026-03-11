using System.Text;

namespace BensEngineeringMetrics;

public class ConsoleReadableOutputter : IReadableOutputter
{
    private readonly StringBuilder buffer = new();

    public void WriteLine(string? message)
    {
        Console.WriteLine(message);
        this.buffer.AppendLine(message);
    }

    public void WriteLine(object someObject)
    {
        Console.WriteLine(someObject);
        this.buffer.AppendLine(someObject.ToString());
    }

    public void WriteLine()
    {
        Console.WriteLine();
        this.buffer.AppendLine();
    }

    public string ReadTextAndResetBuffer()
    {
        var result = this.buffer.ToString();
        this.buffer.Clear();
        return result;
    }

    public override string ToString()
    {
        return this.buffer.ToString();
    }
}
