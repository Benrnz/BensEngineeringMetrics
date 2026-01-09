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

    private async Task ExportShouldBeSyncedTickets(IReadOnlyList<JiraIssue> allSyncedIssues)
    {
        // Get all tickets that are linked to Initiatives and PM Plans
        var (_, allPmPlans) = await jiraRepo.OpenPmPlans();
        var jqlEnvestPmPlans = $"""project = "PMPLAN" AND type = idea AND status NOT IN ("Feature delivered", Cancelled) AND "PM Customer[Checkboxes]" = Envest ORDER BY key""";
        Console.WriteLine(jqlEnvestPmPlans);
        var envestPmPlansKeys = (await runner.SearchJiraIssuesWithJqlAsync(jqlEnvestPmPlans, [JiraFields.PmPlanCustomer])).Select<dynamic, string>(d => JiraFields.Key.Parse(d));
        var envestPmPlans = allPmPlans.Where(p => envestPmPlansKeys.Contains(p.Key));
        var tickets = envestPmPlans
            .SelectMany(p => p.ChildTickets.Select(leaf => new JiraIssue(leaf.Key, leaf.IssueType, "Envest", p.Key)))
            .ToList();

        // Get any ticket that is directly marked as Envest as the Customer
        var jqlDirectEnvestTickets = $"type IN ('{Constants.BugType}', '{Constants.StoryType}', '{Constants.ImprovementType}', '{Constants.SchemaTaskType}') AND \"Customer/s (Multi Select)[Select List (multiple choices)]\" = Envest AND status != Done ORDER BY key";
        Console.WriteLine(jqlDirectEnvestTickets);
        var directEnvestTickets = (await runner.SearchJiraIssuesWithJqlAsync(jqlDirectEnvestTickets, Fields))
            .Select(JiraIssue.CreateJiraIssue)
            .ToList();
        tickets.AddRange(directEnvestTickets.ToList());
        tickets = tickets.DistinctBy(j => j.Key).OrderBy(j => j.Key).ToList();

        // Add in Exalate tag for those synced.
        var mergedList = new List<JiraIssue>();
        foreach (var issue in tickets)
        {
            var syncedIssue = allSyncedIssues.FirstOrDefault(i => i.Key == issue.Key);
            if (syncedIssue is null)
            {
                mergedList.Add(issue);
            }
            else
            {
                mergedList.Add(issue with { Exalate = syncedIssue.Exalate });
            }
        }

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}-ShouldBeSyncedTickets");
        var filename = exporter.Export(mergedList);
        await sheetUpdater.ImportFile($"'{ShouldBeSyncedTicketsSheetName}'!A1", filename);
    }

    private async Task<IReadOnlyList<JiraIssue>> ExportAllSyncedTickets()
    {
        var ticketsSyncedJql = """ "Exalate[Short text]" ~ Envest""";
        Console.WriteLine(ticketsSyncedJql);
        var issues = (await runner.SearchJiraIssuesWithJqlAsync(ticketsSyncedJql, Fields))
            .Select(JiraIssue.CreateJiraIssue)
            .OrderBy(j => j.Key)
            .ToList();
        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}-SyncedTickets");
        var filename = exporter.Export(issues);
        await sheetUpdater.ImportFile($"'{AllSyncedTicketsSheetName}'!A1", filename);
        return issues;
    }

    private record JiraIssue(
        string Key,
        string IssueType,
        string Customer,
        string PmPlanKey = "",
        string Exalate = "") : IJiraKeyedIssue
    {
        public static JiraIssue CreateJiraIssue(dynamic d)
        {
            var labels = (string)JiraFields.Labels.Parse(d);

            return new JiraIssue(
                JiraFields.Key.Parse(d),
                JiraFields.IssueType.Parse(d),
                JiraFields.CustomersMultiSelect.Parse(d),
                "",
                JiraFields.Exalate.Parse(d));
        }
    }
}
