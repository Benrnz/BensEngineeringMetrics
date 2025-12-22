using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BensEngineeringMetrics.Jira;

public class JiraApiClient(bool enableRecording = false)
{
    private const string BaseApi3Url = "https://javlnsupport.atlassian.net/rest/api/3/";
    private const string BaseAgileUrl = "https://javlnsupport.atlassian.net/rest/agile/1.0/";
    private string? logFilePath;

    public async Task<string> GetAgileBoardActiveSprintAsync(int boardId)
    {
        return await GetAgileBoardSprintsAsync(boardId, "active");
    }

    public async Task<string> GetAgileBoardAllSprintsAsync(int boardId, int? startAt = null, int? maxResults = null)
    {
        var sb = new StringBuilder($"{BaseAgileUrl}board/{boardId}/sprint");

        // Build query parameters cleanly using a list
        var queryParts = new List<string>();

        if (startAt.HasValue)
        {
            queryParts.Add($"startAt={startAt.Value}");
        }

        if (maxResults.HasValue)
        {
            queryParts.Add($"maxResults={maxResults.Value}");
        }

        if (queryParts.Any())
        {
            sb.Append('?').Append(string.Join('&', queryParts));
        }

        var response = await App.HttpJira.GetAsync(sb.ToString());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetAgileBoardSprintByIdAsync(int sprintId)
    {
        var response = await App.HttpJira.GetAsync($"{BaseAgileUrl}sprint/{sprintId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> PostSearchJqlAsync(string jql, string[] fields, string? nextPageToken = null)
    {
        var requestBody = new
        {
            expand = "names",
            fields,
            jql,
            maxResults = 500,
            nextPageToken
        };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await App.HttpJira.PostAsync($"{BaseApi3Url}search/jql", content);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("ERROR!");
            Console.WriteLine(response.StatusCode);
            Console.WriteLine(response.ReasonPhrase);
            Console.WriteLine(json);
        }

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();

        if (enableRecording)
        {
            await RecordCallAsync(jql, responseJson);
        }

        return responseJson;
    }

    private async Task RecordCallAsync(string jql, string responseJson)
    {
        // Initialize log file path on first call
        if (this.logFilePath is null)
        {
            var logsDirectory = Path.Combine(App.DefaultFolder, "Logs");
            Directory.CreateDirectory(logsDirectory);

            // Sanitize JQL for filename (first 30 characters)
            var jqlPrefix = jql.Length > 30 ? jql.Substring(0, 30) : jql;
            var sanitizedJql = SanitizeFileName(jqlPrefix);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-ffff");
            var fileName = $"{timestamp}_{sanitizedJql}.json";
            this.logFilePath = Path.Combine(logsDirectory, fileName);
        }

        // Create log entry
        var logEntry = new
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            jql,
            response = responseJson
        };

        var logJson = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = true });

        // Append to file
        await File.AppendAllTextAsync(this.logFilePath, logJson + Environment.NewLine + Environment.NewLine);
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        // Replace spaces and other problematic characters
        sanitized = Regex.Replace(sanitized, @"\s+", "_");
        sanitized = Regex.Replace(sanitized, @"[^\w\-_]", "_");

        return sanitized;
    }

    // New private helper: get sprints for a specific state with paging
    private async Task<string> GetAgileBoardSprintsAsync(int boardId, string state, int? startAt = null, int? maxResults = null)
    {
        var sb = new StringBuilder($"{BaseAgileUrl}board/{boardId}/sprint");
        var queryParts = new List<string>
        {
            $"state={Uri.EscapeDataString(state)}"
        };

        if (startAt.HasValue)
        {
            queryParts.Add($"startAt={startAt.Value}");
        }

        if (maxResults.HasValue)
        {
            queryParts.Add($"maxResults={maxResults.Value}");
        }

        if (queryParts.Any())
        {
            sb.Append('?').Append(string.Join('&', queryParts));
        }

        var response = await App.HttpJira.GetAsync(sb.ToString());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
