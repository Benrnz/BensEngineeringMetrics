using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Test.TestHarnesses;

public class JiraApiClientTestHarness(string testLogsName) : JiraApiClient
{
    private List<LogEntry>? cachedLogEntries;
    private string? cachedLogsDirectory;
    private DateTime lastTimestamp = DateTime.MinValue;

    public override async Task<string> PostSearchJqlAsync(string jql, string[] fields, string? nextPageToken = null)
    {
        // Load and cache all log entries on first call
        if (this.cachedLogEntries is null)
        {
            await LoadAllLogFilesAsync();
        }

        // Find matching entry from cached entries
        Console.WriteLine($"Looking for JQL: '{jql}' pageToken: {nextPageToken ?? "null"}");
        LogEntry? matchingEntry;
        if (this.lastTimestamp == DateTime.MinValue)
        {
            matchingEntry = this.cachedLogEntries!.First();
        }
        else
        {
            matchingEntry = this.cachedLogEntries!.FirstOrDefault(e => e.Timestamp > this.lastTimestamp);
        }

        if (matchingEntry is not null)
        {
            Console.WriteLine("    Success");
            this.lastTimestamp = matchingEntry.Timestamp;
            if (matchingEntry.Jql != jql)
            {
                throw new InvalidOperationException($"JQL '{jql}' does not match next recorded entry {matchingEntry.Jql}.");
            }

            return matchingEntry.Response;
        }

        throw new InvalidOperationException($"No matching log entry found for JQL: {jql}, nextPageToken: {nextPageToken ?? "null"}");
    }

    private async Task LoadAllLogFilesAsync()
    {
        // Get the logs directory path - logs are in the test project directory
        if (this.cachedLogsDirectory is null)
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

            // Navigate from bin/Debug/net9.0 back to test project root
            var testProjectDirectory = assemblyDirectory is not null
                ? Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "..", ".."))
                : AppContext.BaseDirectory;

            this.cachedLogsDirectory = Path.Combine(testProjectDirectory, testLogsName);
        }

        if (!Directory.Exists(this.cachedLogsDirectory))
        {
            throw new DirectoryNotFoundException($"Logs directory not found: {this.cachedLogsDirectory}");
        }

        // Load all log files
        var logFiles = Directory.GetFiles(this.cachedLogsDirectory, "*.json");
        var allEntries = new List<LogEntry>();

        foreach (var logFile in logFiles)
        {
            var entries = await ParseLogFileAsync(logFile);
            allEntries.AddRange(entries);
        }

        this.cachedLogEntries = allEntries.OrderBy(e => e.Timestamp).ToList();
    }

    private async Task<List<LogEntry>> ParseLogFileAsync(string logFilePath)
    {
        var entries = new List<LogEntry>();
        var content = await File.ReadAllTextAsync(logFilePath);

        // Split by double newlines to get individual entries
        var entryStrings = content.Split([Environment.NewLine + Environment.NewLine, "\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new DateTimeConverter() }
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
        public string Jql { get; set; } = string.Empty;
        public string? NextPageToken { get; set; }
        public string Response { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.MaxValue;
    }

    private class DateTimeConverter : JsonConverter<DateTime>
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                if (DateTime.TryParseExact(stringValue, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                {
                    return dateTime;
                }
            }

            throw new JsonException($"Unable to convert \"{reader.GetString()}\" to DateTime.");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(DateTimeFormat));
        }
    }
}
