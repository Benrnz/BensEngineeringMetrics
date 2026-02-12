using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

public class InitiativeProgressTableTask(IJiraQueryRunner runner, IWorkSheetReader sheetReader, IWorkSheetUpdater sheetUpdater) : IEngineeringMetricsTask
{
    private const string GoogleSheetId = "1OVUx08nBaD8uH-klNAzAtxFSKTOvAAk5Vnm11ALN0Zo";
    private const string TaskKey = "INIT_TABLE";
    private const string ProductInitiativePrefix = "PMPLAN-";

    private static readonly IFieldMapping[] IssueFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.ParentKey,
        JiraFields.StoryPoints,
        JiraFields.OriginalEstimate,
        JiraFields.Created,
        JiraFields.Resolved,
        JiraFields.Team,
        JiraFields.Sprint
    ];

    private static readonly IFieldMapping[] PmPlanFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IssueType,
        JiraFields.PmPlanHighLevelEstimate,
        JiraFields.EstimationStatus,
        JiraFields.ProjectTarget
    ];

    public IList<JiraInitiative> AllInitiativesData { get; private set; } = new List<JiraInitiative>();

    public IDictionary<string, IReadOnlyList<JiraIssue>> AllIssuesData { get; private set; } = new Dictionary<string, IReadOnlyList<JiraIssue>>();

    public string Description => "Export and update Initiative level PMPLAN data for drawing feature-set release _burn-up_charts_";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine($"{Key} - {Description}");

        await LoadData();
        await sheetUpdater.Open(GoogleSheetId);

        // Update the Summary Tab
        var summaryReportArray = BuildSummaryReportArray(AllInitiativesData);
        sheetUpdater.ClearRange("Summary", "A2:Z10000");
        sheetUpdater.EditSheet("'Summary'!A2", summaryReportArray, true);

        // Update the OverviewGraph tab
        var overviewReportArray = BuildOverviewReportArray(AllInitiativesData);
        sheetUpdater.ClearRange("OverviewGraphs", "A2:Z10000");
        sheetUpdater.EditSheet("'OverviewGraphs'!A2", overviewReportArray, true);
        sheetUpdater.EditSheet("Info!B1", [[DateTime.Now.ToString("g")]]);
        await sheetUpdater.SubmitBatch();
    }

    public async Task LoadData()
    {
        if (AllInitiativesData.Any())
        {
            // Data already loaded
            return;
        }

        await sheetReader.Open(GoogleSheetId);
        var initiativeKeys = await GetInitiativesForReport();
        if (!initiativeKeys.Any())
        {
            Console.WriteLine("No Product Initiatives found to process. Exiting.");
            return;
        }

        await ExtractAllInitiativeData(initiativeKeys);
    }

    private static IList<IList<object?>> BuildOverviewReportArray(IList<JiraInitiative> allInitiativeData)
    {
        // This data excludes UAT so that the overall initiative can be reports as a 'Done' state even if the block of time allocated to UAT is not completely used up.
        // 4 columns: Name, Key, Done, Remaining
        IList<IList<object?>> reportArray = new List<IList<object?>>();
        foreach (var initiative in allInitiativeData)
        {
            var pmPlansExclUat = initiative.PmPlans.Where(p => !p.Description.StartsWith("UAT:")).ToList();
            var row = new List<object?>
            {
                initiative.Description,
                //https://javlnsupport.atlassian.net/jira/polaris/projects/PMPLAN/ideas/view/6464278?selectedIssue=PMPLAN-204&issueViewSection=deliver
                JiraUtil.HyperlinkDiscoTicket(initiative.InitiativeKey),
                pmPlansExclUat.Sum(p => p.Progress.DoneStoryPoints),
                pmPlansExclUat.Sum(p => p.Progress.RemainingStoryPoints)
            };
            reportArray.Add(row);
        }

        return reportArray;
    }

    private static IList<IList<object?>> BuildSummaryReportArray(IList<JiraInitiative> allInitiativeData)
    {
        IList<IList<object?>> reportArray = new List<IList<object?>>();
        foreach (var initiative in allInitiativeData)
        {
            var row = new List<object?>
            {
                initiative.Description,
                JiraUtil.HyperlinkDiscoTicket(initiative.InitiativeKey),
                initiative.Progress.TotalStoryPoints,
                initiative.Progress.DoneStoryPoints,
                initiative.Progress.RemainingStoryPoints,
                initiative.Progress.RemainingTicketCount,
                initiative.Progress.PercentDone,
                initiative.Status,
                initiative.Target?.ToString("d MMM yy")
            };
            reportArray.Add(row);
            foreach (var childPmPlan in initiative.PmPlans)
            {
                var childRow = new List<object?>
                {
                    childPmPlan.Description,
                    JiraUtil.HyperlinkDiscoTicket(childPmPlan.PmPlanKey),
                    childPmPlan.Progress.TotalStoryPoints,
                    childPmPlan.Progress.DoneStoryPoints,
                    childPmPlan.Progress.RemainingStoryPoints,
                    childPmPlan.Progress.RemainingTicketCount,
                    childPmPlan.Progress.PercentDone,
                    childPmPlan.Status,
                    childPmPlan.Target?.ToString("d MMM yy")
                };
                reportArray.Add(childRow);
            }

            // Spacer empty row
            reportArray.Add(new List<object?>());
        }

        return reportArray;
    }

    private JiraIssue CreateJiraIssue(string? pmPlan, string? pmPlanSummary, dynamic issue)
    {
        string status = JiraFields.Status.Parse(issue) ?? Constants.Unknown;
        double storyPoints = JiraFields.StoryPoints.Parse(issue) ?? 0.0;

        return new JiraIssue(
            JiraFields.Key.Parse(issue)!,
            JiraFields.Created.Parse(issue),
            JiraFields.Resolved.Parse(issue),
            status,
            storyPoints,
            pmPlan ?? string.Empty,
            JiraFields.Summary.Parse(issue) ?? string.Empty,
            pmPlanSummary ?? string.Empty,
            JiraFields.Team.Parse(issue) ?? string.Empty,
            JiraFields.Sprint.Parse(issue) ?? string.Empty);
    }

    private async Task ExtractAllInitiativeData(IReadOnlyList<string> initiativeKeys)
    {
        var pmPlanJql = "(issue in linkedIssues(\"{0}\") OR parent in linkedIssues(\"{0}\")) AND \"Required for Go-live[Checkbox]\" = 1 ORDER BY key";
        var javPmKeyql = "project = JAVPM AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";

        var allInitiativeData = new List<JiraInitiative>();
        AllIssuesData = new Dictionary<string, IReadOnlyList<JiraIssue>>();
        foreach (var initiative in initiativeKeys)
        {
            Console.WriteLine($"* Finding all work for {initiative}");
            var jiraInitiative = await GetInitiativeDetails(initiative);
            var pmPlans = await runner.SearchJiraIssuesWithJqlAsync(string.Format(pmPlanJql, initiative), PmPlanFields);

            var allIssues = new List<JiraIssue>();
            foreach (var pmPlan in pmPlans)
            {
                string pmPlanKey = JiraFields.Key.Parse(pmPlan);
                string summary = JiraFields.Summary.Parse(pmPlan) ?? string.Empty;
                string status = JiraFields.Status.Parse(pmPlan) ?? Constants.Unknown;
                DateTimeOffset? target = JiraFields.ProjectTarget.Parse(pmPlan);
                var pmPlanData = new JiraPmPlan(pmPlanKey, summary, new StatLine(), status, target);
                var children = await runner.SearchJiraIssuesWithJqlAsync(string.Format(javPmKeyql, pmPlanKey), IssueFields);
                Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
                var range = children.Select<dynamic, JiraIssue>(i => CreateJiraIssue(pmPlanKey, summary, i)).ToList();
                pmPlanData.Progress.TotalStoryPoints = range.Sum(i => i.StoryPoints);
                pmPlanData.Progress.RemainingTicketCount = range.Count(i => i.Status != Constants.DoneStatus);
                pmPlanData.Progress.DoneStoryPoints = range.Where(i => i.Status == Constants.DoneStatus).Sum(i => i.StoryPoints);
                allIssues.AddRange(range);
                jiraInitiative.PmPlans.Add(pmPlanData);
            }

            Console.WriteLine($"Found {allIssues.Count} unique stories");
            jiraInitiative.Progress.TotalStoryPoints = jiraInitiative.PmPlans.Sum(p => p.Progress.TotalStoryPoints);
            jiraInitiative.Progress.DoneStoryPoints = jiraInitiative.PmPlans.Sum(p => p.Progress.DoneStoryPoints);
            jiraInitiative.Progress.RemainingTicketCount = jiraInitiative.PmPlans.Sum(p => p.Progress.RemainingTicketCount);
            AllIssuesData.Add(initiative, allIssues);
            allInitiativeData.Add(jiraInitiative);
        } // For each initiative

        AllInitiativesData = allInitiativeData;
    }

    private async Task<JiraInitiative> GetInitiativeDetails(string initiativeKey)
    {
        var result = await runner.SearchJiraIssuesWithJqlAsync($"key={initiativeKey}", [JiraFields.Summary, JiraFields.Status, JiraFields.ProjectTarget]);
        var single = result.Single();
        string summary = JiraFields.Summary.Parse(single) ?? string.Empty;
        string status = JiraFields.Status.Parse(single) ?? Constants.Unknown;
        DateTimeOffset? target = JiraFields.ProjectTarget.Parse(single);
        return new JiraInitiative(initiativeKey, summary, new List<JiraPmPlan>(), new StatLine(), status, target);
    }

    private async Task<IReadOnlyList<string>> GetInitiativesForReport()
    {
        var list = await sheetReader.GetSheetNames();
        var initiatives = list.Where(x => x.StartsWith(ProductInitiativePrefix)).ToList();
        Console.WriteLine("Updating burn-up charts for the following Product Initiatives:");
        foreach (var initiative in initiatives)
        {
            Console.WriteLine($"*   {initiative}");
        }

        return initiatives;
    }

    public record JiraInitiative(string InitiativeKey, string Description, IList<JiraPmPlan> PmPlans, StatLine Progress, string Status, DateTimeOffset? Target = null);

    public record JiraPmPlan(string PmPlanKey, string Description, StatLine Progress, string Status, DateTimeOffset? Target = null);

    public record StatLine
    {
        public double RemainingTicketCount { get; set; }

        public double DoneStoryPoints { get; set; }

        public double PercentDone
        {
            get
            {
                if (RemainingStoryPoints == 0 && RemainingTicketCount > 0)
                {
                    return 0.99;
                }

                return DoneStoryPoints / (TotalStoryPoints == 0 ? 1 : TotalStoryPoints);
            }
        }

        public double RemainingStoryPoints => TotalStoryPoints - DoneStoryPoints;
        public double TotalStoryPoints { get; set; }
    }

    public record JiraIssue(
        string Key,
        DateTimeOffset CreatedDateTime,
        DateTimeOffset? ResolvedDateTime,
        string Status,
        double StoryPoints,
        string PmPlan,
        string Summary,
        string PmPlanSummary,
        string Team,
        string Sprint);
}
