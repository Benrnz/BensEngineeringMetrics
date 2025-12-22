using System.Text.Json;
using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Test.TestHarnesses;

public class JiraApiClientTestHarness : JiraApiClient
{
    private List<LogEntry>? cachedLogEntries;
    private string? cachedLogsDirectory;

    public override async Task<string> PostSearchJqlAsync(string jql, string[] fields, string? nextPageToken = null)
    {
        // Load and cache all log entries on first call
        if (this.cachedLogEntries is null)
        {
            await LoadAllLogFilesAsync();
        }

        // Find matching entry from cached entries
        var matchingEntry = this.cachedLogEntries!.FirstOrDefault(e => e.Jql == jql && e.NextPageToken == nextPageToken);

        if (matchingEntry is not null)
        {
            return matchingEntry.Response;
        }

        throw new InvalidOperationException($"No matching log entry found for JQL: {jql}, nextPageToken: {nextPageToken ?? "null"}");
    }

    private async Task LoadAllLogFilesAsync()
    {
        // Get the logs directory path - logs are in the test project directory
        if (this.cachedLogsDirectory is null)
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

            // Navigate from bin/Debug/net9.0 back to test project root
            var testProjectDirectory = assemblyDirectory is not null
                ? Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "..", ".."))
                : AppContext.BaseDirectory;

            this.cachedLogsDirectory = Path.Combine(testProjectDirectory, "CalculatePmPlanReleasesBurnUpValuesLogs");
        }

        if (!Directory.Exists(this.cachedLogsDirectory))
        {
            throw new DirectoryNotFoundException($"Logs directory not found: {this.cachedLogsDirectory}");
        }

        // Load all log files
        var logFiles = Directory.GetFiles(this.cachedLogsDirectory, "*.log");
        var allEntries = new List<LogEntry>();

        foreach (var logFile in logFiles)
        {
            var entries = await ParseLogFileAsync(logFile);
            allEntries.AddRange(entries);
        }

        this.cachedLogEntries = allEntries;
    }

    private async Task<List<LogEntry>> ParseLogFileAsync(string logFilePath)
    {
        var entries = new List<LogEntry>();
        var content = await File.ReadAllTextAsync(logFilePath);

        // Split by double newlines to get individual entries
        var entryStrings = content.Split(new[] { Environment.NewLine + Environment.NewLine, "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        foreach (var entryString in entryStrings)
        {
            try
            {
                var entry = JsonSerializer.Deserialize<LogEntry>(entryString.Trim(), options);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // Skip malformed entries
            }
        }

        return entries;
    }

    private class LogEntry
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Jql { get; set; } = string.Empty;
        public string? NextPageToken { get; set; }
        public string Response { get; set; } = string.Empty;
    }
}
