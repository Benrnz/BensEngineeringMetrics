using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

/// <summary>
///     See https://javlnsupport.atlassian.net/wiki/spaces/DEVELOPMEN/pages/1284243457/Engineering+Tasks+Summary+-+November+2025
/// </summary>
public class ExportEngineeringTaskAnalysis(IJiraQueryRunner runner, IWorkSheetUpdater sheetUpdater, ICsvExporter exporter) : IEngineeringMetricsTask
{
    private const string GoogleSheetId = "1_ANmhfs-kjyCwntbeS2lM0YYOpeBgCDoasZz5UZpl2g";
    private const string TaskKey = "ENG_TASK_ANALYSIS";
    private const string PiechartSheetTab = "Latest Piecharts";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.IssueType,
        JiraFields.Summary,
        JiraFields.Created,
        JiraFields.UpdatedDate,
        JiraFields.StoryPoints,
        JiraFields.WorkDoneBy,
        JiraFields.Labels
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

        this.startDate = DateUtils.StartOfMonth(DateTimeOffset.Now.AddMonths(-1));
        this.endDate = DateUtils.EndOfMonth(this.startDate);

        await sheetUpdater.Open(GoogleSheetId);

        await CreateMonthTicketSheet();
        CreatePieChartData();

        await sheetUpdater.SubmitBatch();
    }

    private async Task CreateMonthTicketSheet()
    {
        var month = this.startDate.ToString("MMM");
        var jql =
            $"project in (JAVPM,ENG) and type != EPIC  AND statusCategoryChangedDate >= '{this.startDate:yyyy-MM-dd}' and statusCategory = Done  and  statusCategoryChangedDate < '{this.endDate:yyyy-MM-dd}'";
        Console.WriteLine(jql);

        this.issues = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(JiraIssue.CreateJiraIssue).ToList();

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}_{month}_Issues");
        var fileName = exporter.Export(this.issues);
        var sheetName = $"{month} JiraIssues";
        if (await sheetUpdater.DoesSheetExist(sheetName))
        {
            sheetUpdater.ClearRange(sheetName);
        }
        else
        {
            sheetUpdater.AddSheet(sheetName);
        }

        await sheetUpdater.ImportFile($"{sheetName}!A1", fileName);
    }

    private void CreatePieChartData()
    {
        sheetUpdater.ClearRange($"{PiechartSheetTab}", "A1:D100");

        var chartData = new List<IList<object?>>
        {
            new List<object?> { "Date Range", this.startDate.ToString("d-MMM-yy"), this.endDate.ToString("d-MMM-yy") }
        };
        sheetUpdater.EditSheet($"'{PiechartSheetTab}'!A1", chartData, true);

        var totalCount = (double)this.issues.Count;
        if (totalCount == 0)
        {
            Console.WriteLine("No data returned.");
            return;
        }

        var totalPoints = this.issues.Sum(i => i.StoryPoints == 0 ? 1 : i.StoryPoints);
        InsertTicketTypeAnalysisTable(totalCount, totalPoints);
        InsertEngineeringExcellenceTable(totalCount, totalPoints);
    }

    private void InsertEngineeringExcellenceTable(double totalCount, double totalPoints)
    {
        var data = this.issues
            .Where(i => i.Labels.Contains(Constants.EngineeringExcellenceLabel))
            .ToList();
        var excellenceCount = (double)data.Count;
        var excellencePoints = data.Sum(i => i.StoryPoints == 0 ? 1 : i.StoryPoints);

        data = this.issues
            .Where(i => i.IssueType == Constants.BugType)
            .ToList();
        var bugCount = (double)data.Count;
        var bugPoints = data.Sum(i => i.StoryPoints == 0 ? 1 : i.StoryPoints);

        var roadMapCount = totalCount - excellenceCount - bugCount;
        var roadMapPoints = totalPoints - excellencePoints - bugPoints;

        List<IList<object?>> chartData =
        [
            new List<object?> { "Ticket Type", "Percentage", "Ticket Count", "Story Points Percentage" },
            new List<object?> { "Engineering Excellence", excellenceCount / totalCount, excellenceCount, excellencePoints / totalPoints },
            new List<object?> { "Bugs", bugCount / totalCount, bugCount, bugPoints / totalPoints },
            new List<object?> { "Product Roadmap", roadMapCount / totalCount, roadMapCount, roadMapPoints / totalPoints }
        ];

        sheetUpdater.EditSheet($"'{PiechartSheetTab}'!A49", chartData, true);
        sheetUpdater.BoldCellsFormat(PiechartSheetTab, 48, 49, 0, 4);
    }

    private void InsertTicketTypeAnalysisTable(double totalCount, double totalPoints)
    {
        var data = this.issues
            .GroupBy(i => i.IssueType)
            .Select<IGrouping<string, JiraIssue>, EngineeringTicketTypeChart>(g =>
                new EngineeringTicketTypeChart(
                    g.Key,
                    g.Count() / totalCount,
                    g.Count(),
                    g.Sum(i => i.StoryPoints == 0 ? 1 : i.StoryPoints) / totalPoints
                ))
            .OrderByDescending(g => g.Count)
            .ToList();

        List<IList<object?>> chartData = [new List<object?> { "Ticket Type", "Percentage", "Ticket Count", "Story Points Percentage" }];

        foreach (var group in data)
        {
            chartData.Add(new List<object?>
            {
                group.TicketType,
                group.Percentage,
                group.Count,
                group.StoryPointsPercentage
            });
        }

        chartData.Add(new List<object?> { "Total", null, totalCount, totalPoints });

        sheetUpdater.EditSheet($"'{PiechartSheetTab}'!A3", chartData, true);
        sheetUpdater.BoldCellsFormat(PiechartSheetTab, 2, 3, 0, 4);
        sheetUpdater.BoldCellsFormat(PiechartSheetTab, chartData.Count + 1, chartData.Count + 2, 0, 4);
    }

    private record EngineeringTicketTypeChart(string TicketType, double Percentage, int Count, double StoryPointsPercentage);

    private record JiraIssue(
        string Key,
        string Summary,
        string IssueType,
        DateTimeOffset Created,
        DateTimeOffset UpdatedDate,
        double StoryPoints,
        string WorkDoneBy,
        string[] Labels)
    {
        public static JiraIssue CreateJiraIssue(dynamic d)
        {
            var workDoneBy = JiraFields.WorkDoneBy.Parse(d);
            var labels = (string)JiraFields.Labels.Parse(d);

            return new JiraIssue(
                JiraFields.Key.Parse(d),
                JiraFields.Summary.Parse(d),
                JiraFields.IssueType.Parse(d),
                JiraFields.Created.Parse(d),
                JiraFields.UpdatedDate.Parse(d),
                JiraFields.StoryPoints.Parse(d),
                workDoneBy,
                labels.Split(','));
        }
    }
}
