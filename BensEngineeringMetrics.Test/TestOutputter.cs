using System.Text;
using Xunit.Abstractions;

namespace BensEngineeringMetrics.Test;

public class TestOutputter : IOutputter
{
    private readonly ITestOutputHelper testOutputHelper;
    private readonly StringBuilder entireLog = new();

    public TestOutputter(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
    }

    public void WriteLine(string? message)
    {
        this.testOutputHelper.WriteLine(message ?? string.Empty);
        this.entireLog.AppendLine(message ?? string.Empty);
    }

    public void WriteLine(object someObject)
    {
        var message = someObject?.ToString() ?? string.Empty;
        this.testOutputHelper.WriteLine(message);
        this.entireLog.AppendLine(message);
    }

    public void WriteLine()
    {
        this.testOutputHelper.WriteLine(string.Empty);
        this.entireLog.AppendLine(string.Empty);
    }

    public string GetEntireLog()
    {
        return this.entireLog.ToString();
    }
}
