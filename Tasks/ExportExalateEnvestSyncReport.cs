using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

public class ExportExalateEnvestSyncReport(IJiraQueryRunner runner, IWorkSheetUpdater sheetUpdater, ICsvExporter exporter, IJiraIssueRepository jiraRepo) : IEngineeringMetricsTask
{
    private const string GoogleSheetId = "1irosbf4piwZnRSW6nzGAWu_8qwNhm4KAaISyHCpoaNI";
    private const string TaskKey = "ENVEST_EXALATE";
    private const string AllSyncedTicketsSheetName = "Tickets Synced to Envest";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.IssueType,
        JiraFields.Summary,
        JiraFields.Created,
        JiraFields.StoryPoints,
        JiraFields.Team,
        JiraFields.Exalate
    ];

    public string Description => "A report to show tickets that are Sync'ed with Envest via Exalate, and those that likely should be.";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine($"{Key} - {Description}");
        Console.WriteLine();

        await sheetUpdater.Open(GoogleSheetId);

        await ExportAllSyncedTickets();

        var (mappedInitiatives, pmPlans) = await jiraRepo.OpenPmPlans();


        sheetUpdater.EditSheet("Info!B1", [[DateTime.Now.ToString("g")]]);

        await sheetUpdater.SubmitBatch();
    }

    private async Task ExportAllSyncedTickets()
    {
        var ticketsSyncedJql = """ "Exalate[Short text]" ~ Envest""";
        var issues = (await runner.SearchJiraIssuesWithJqlAsync(ticketsSyncedJql, Fields)).Select(JiraIssue.CreateJiraIssue);
        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}-SyncedTickets");
        var filename = exporter.Export(issues);
        await sheetUpdater.ImportFile($"'{AllSyncedTicketsSheetName}'!A1", filename);
    }

    private record JiraIssue(
        string Key,
        string Summary,
        string IssueType,
        DateTimeOffset Created,
        double StoryPoints,
        string Team,
        string PmPlanInitiativeKey = "") : IJiraKeyedIssue
    {
        public static JiraIssue CreateJiraIssue(dynamic d)
        {
            var labels = (string)JiraFields.Labels.Parse(d);

            return new JiraIssue(
                JiraFields.Key.Parse(d),
                JiraFields.Summary.Parse(d),
                JiraFields.IssueType.Parse(d),
                JiraFields.Created.Parse(d),
                JiraFields.StoryPoints.Parse(d),
                JiraFields.Team.Parse(d));
        }
    }
}
