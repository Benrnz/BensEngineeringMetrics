using System.Text;
using Xunit.Abstractions;

namespace BensEngineeringMetrics.Test;

public class TestOutputter : IOutputter
{
    private readonly StringBuilder entireLog = new();
    private readonly ITestOutputHelper testOutputHelper;

    public TestOutputter(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
    }

    public void WriteLine(string? message)
    {
        if (message == null)
        {
            return;
        }

        this.testOutputHelper.WriteLine(message ?? string.Empty);
        this.entireLog.AppendLine(message ?? string.Empty);
    }

    public void WriteLine(object? someObject)
    {
        if (someObject == null)
        {
            return;
        }

        var message = someObject?.ToString() ?? string.Empty;
        this.testOutputHelper.WriteLine(message);
        this.entireLog.AppendLine(message);
    }

    public void WriteLine()
    {
        this.testOutputHelper.WriteLine(string.Empty);
        this.entireLog.AppendLine(string.Empty);
    }

    public void Write(string? message)
    {
        if (message == null)
        {
            return;
        }

        this.testOutputHelper.WriteLine(message ?? string.Empty);
        this.entireLog.Append(message ?? string.Empty);
    }

    public void Write(object? someObject)
    {
        this.testOutputHelper.WriteLine(someObject?.ToString());
        this.entireLog.Append(someObject);
    }

    public string GetEntireLog()
    {
        return this.entireLog.ToString();
    }
}
