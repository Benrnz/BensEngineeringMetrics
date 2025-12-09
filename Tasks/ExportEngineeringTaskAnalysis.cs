using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

/// <summary>
///     See https://javlnsupport.atlassian.net/wiki/spaces/DEVELOPMEN/pages/1284243457/Engineering+Tasks+Summary+-+November+2025
/// </summary>
/// <param name="runner"></param>
/// <param name="sheetUpdater"></param>
/// <param name="exporter"></param>
public class ExportEngineeringTaskAnalysis(IJiraQueryRunner runner, IWorkSheetUpdater sheetUpdater, ICsvExporter exporter) : IEngineeringMetricsTask
{
    private const string GoogleSheetId = "1_ANmhfs-kjyCwntbeS2lM0YYOpeBgCDoasZz5UZpl2g";
    private const string TaskKey = "ENG_TASK_ANALYSIS";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.IssueType,
        JiraFields.Summary,
        JiraFields.Created,
        JiraFields.UpdatedDate,
        JiraFields.StoryPoints,
        JiraFields.WorkDoneBy
    ];

    private DateTimeOffset endDate = DateTimeOffset.Now;

    private IReadOnlyList<JiraIssue> issues = new List<JiraIssue>();

    private DateTimeOffset startDate = DateTimeOffset.Now;

    public string Description => "Export to a Google Sheet the last month of tasks through Engineering and the split of work type.";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine($"{Key} - {Description}");
        Console.WriteLine();

        await CreateMonthTicketSheet();
    }

    private async Task CreateMonthTicketSheet()
    {
        this.startDate = DateUtils.StartOfMonth(DateTimeOffset.Now.AddMonths(-1));
        this.endDate = DateUtils.EndOfMonth(this.startDate);
        var month = this.startDate.ToString("MMM");
        var jql =
            $"project in (JAVPM,ENG) and type != EPIC  AND statusCategoryChangedDate >= '{this.startDate:yyyy-MM-dd}' and statusCategory = Done  and  statusCategoryChangedDate < '{this.endDate:yyyy-MM-dd}'";
        Console.WriteLine(jql);

        this.issues = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(JiraIssue.CreateJiraIssue).ToList();

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}_{month}_Issues");
        var fileName = exporter.Export(this.issues);
        await sheetUpdater.Open(GoogleSheetId);
        var sheetName = $"{month} JiraIssues";
        if (await sheetUpdater.DoesSheetExist(GoogleSheetId, sheetName))
        {
            sheetUpdater.ClearRange(sheetName);
        }
        else
        {
            sheetUpdater.AddSheet(sheetName);
            await sheetUpdater.SubmitBatch();
        }

        await sheetUpdater.ImportFile($"{sheetName}!A1", fileName);
        await sheetUpdater.SubmitBatch();
    }

    private record JiraIssue(
        string Key,
        string Summary,
        string IssueType,
        DateTimeOffset Created,
        DateTimeOffset UpdatedDate,
        double StoryPoints,
        string WorkDoneBy)
    {
        public static JiraIssue CreateJiraIssue(dynamic d)
        {
            var workDoneBy = JiraFields.WorkDoneBy.Parse(d);

            return new JiraIssue(
                JiraFields.Key.Parse(d),
                JiraFields.Summary.Parse(d),
                JiraFields.IssueType.Parse(d),
                JiraFields.Created.Parse(d),
                JiraFields.UpdatedDate.Parse(d),
                JiraFields.StoryPoints.Parse(d),
                workDoneBy);
        }
    }
}
