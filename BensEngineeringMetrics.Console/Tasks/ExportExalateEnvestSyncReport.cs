using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

public class ExportExalateEnvestSyncReport(IJiraQueryRunner runner, IWorkSheetUpdater sheetUpdater, ICsvExporter exporter, IJiraIssueRepository jiraRepo) : IEngineeringMetricsTask
{
    private const string GoogleSheetId = "1irosbf4piwZnRSW6nzGAWu_8qwNhm4KAaISyHCpoaNI";
    private const string TaskKey = "ENVEST_EXALATE";
    private const string AllSyncedTicketsSheetName = "Tickets Synced to Envest";
    private const string ShouldBeSyncedTicketsSheetName = "Should be Synced to Envest";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.IssueType,
        JiraFields.Summary,
        JiraFields.Exalate,
        JiraFields.CustomersMultiSelect,
        JiraFields.Team
    ];

    public string Description => "A report to show tickets that are Sync'ed with Envest via Exalate, and those that likely should be.";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine($"{Key} - {Description}");
        Console.WriteLine();

        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.ClearRange(AllSyncedTicketsSheetName);
        sheetUpdater.ClearRange(ShouldBeSyncedTicketsSheetName);

        var allSyncedIssues = await ExportAllSyncedTickets();
        await ExportShouldBeSyncedTickets(allSyncedIssues);

        sheetUpdater.EditSheet("Info!B1", [[DateTime.Now.ToString("g")]]);

        await sheetUpdater.SubmitBatch();
    }

    private async Task<IReadOnlyList<JiraIssue>> ExportAllSyncedTickets()
    {
        var ticketsSyncedJql = """ "Exalate[Short text]" IS NOT EMPTY""";
        Console.WriteLine(ticketsSyncedJql);
        var issues = (await runner.SearchJiraIssuesWithJqlAsync(ticketsSyncedJql, Fields))
            .Select(JiraIssue.CreateJiraIssueWithLinks)
            .OrderBy(j => j.Key)
            .ToList();
        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}-SyncedTickets");
        var filename = exporter.Export(issues);
        await sheetUpdater.ImportFile($"'{AllSyncedTicketsSheetName}'!A1", filename, true);
        return issues;
    }

    private async Task ExportShouldBeSyncedTickets(IReadOnlyList<JiraIssue> allSyncedIssues)
    {
        // Get all tickets that are linked to Initiatives and PM Plans
        var (_, allPmPlans) = await jiraRepo.OpenPmPlans();
        var envestPmPlans = allPmPlans.Where(p => p.Customer.Contains(Constants.Envest));
        var tickets = envestPmPlans
            .SelectMany(p => p.ChildTickets.Select(leaf => new JiraIssue(leaf.Key, leaf.IssueType, "Envest", p.Key)))
            .ToList();

        // Get any ticket that is directly marked as Envest as the Customer
        var jqlDirectEnvestTickets =
            $"type IN ('{Constants.BugType}', '{Constants.StoryType}', '{Constants.ImprovementType}', '{Constants.SchemaTaskType}') AND \"Customer/s (Multi Select)[Select List (multiple choices)]\" = Envest AND status != Done ORDER BY key";
        Console.WriteLine(jqlDirectEnvestTickets);
        var directEnvestTickets = (await runner.SearchJiraIssuesWithJqlAsync(jqlDirectEnvestTickets, Fields))
            .Select(JiraIssue.CreateJiraIssue)
            .ToList();
        tickets.AddRange(directEnvestTickets.ToList());
        tickets = tickets
            .DistinctBy(j => j.Key).OrderBy(j => j.Key)
            .Where(j => j.Key.StartsWith("JAVPM-"))
            .ToList();

        // Add in Exalate tag for those synced and hyperlink key.
        var mergedList = new List<JiraIssue>();
        foreach (var issue in tickets)
        {
            var syncedIssue = allSyncedIssues.FirstOrDefault(i => i.Key == issue.Key);
            var pmPlanHyperlink = string.IsNullOrWhiteSpace(issue.PmPlanKey) ? string.Empty : JiraUtil.HyperlinkDiscoTicket(issue.PmPlanKey);
            if (syncedIssue is null)
            {
                mergedList.Add(issue with { Key = JiraUtil.HyperlinkTicket(issue.Key), PmPlanKey = pmPlanHyperlink, Team = issue.Team });
            }
            else
            {
                mergedList.Add(issue with { Key = JiraUtil.HyperlinkTicket(issue.Key), Exalate = syncedIssue.Exalate, PmPlanKey = pmPlanHyperlink, Team = issue.Team });
            }
        }

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}-ShouldBeSyncedTickets");
        var filename = exporter.Export(mergedList);
        await sheetUpdater.ImportFile($"'{ShouldBeSyncedTicketsSheetName}'!A1", filename, true);
    }

    private record JiraIssue(
        string Key,
        string IssueType,
        string Customer,
        string PmPlanKey = "",
        string Exalate = "",
        string Team = "") : IJiraKeyedIssue
    {
        public static JiraIssue CreateJiraIssue(dynamic d)
        {
            return new JiraIssue(
                JiraFields.Key.Parse(d),
                JiraFields.IssueType.Parse(d),
                JiraFields.CustomersMultiSelect.Parse(d),
                "",
                JiraFields.Exalate.Parse(d),
                JiraFields.Team.Parse(d));
        }

        public static JiraIssue CreateJiraIssueWithLinks(dynamic d)
        {
            return new JiraIssue(
                JiraUtil.HyperlinkTicket(JiraFields.Key.Parse(d)),
                JiraFields.IssueType.Parse(d),
                JiraFields.CustomersMultiSelect.Parse(d),
                "",
                JiraFields.Exalate.Parse(d),
                JiraFields.Team.Parse(d));
        }
    }
}
