using System.Text;
using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

/// <summary>
///     This task retrieves all Jira Issues related to top-level Jira Initiatives for the customer Envest
///     that are marked as "Required For Go-live" but are not yet Done.
/// </summary>
public class RemainingWorkEnvestTask(IJiraIssueRepository jiraIssueRepository, IOutputter outputter, IWorkSheetUpdater sheetUpdater, IJiraQueryRunner runner) : IEngineeringMetricsTask
{
    private const string TaskKey = "REMAINING_WORK_ENVEST";
    private const string GoogleSheetId = "1OVUx08nBaD8uH-klNAzAtxFSKTOvAAk5Vnm11ALN0Zo";
    private const string SheetTabName = "Remaining Work";

    private static readonly IFieldMapping[] Fields = [JiraFields.StoryPoints, JiraFields.Priority, JiraFields.Exalate];

    private IList<EnvestNotDoneIssue> notDoneIssues = new List<EnvestNotDoneIssue>();
    private IList<ExtraData> extraData = new List<ExtraData>();

    public string Description => $"Find all {Constants.Envest} Jira Issues Required For Go-live that are not Done";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        outputter.WriteLine($"{Key} - {Description}");

        await LoadData();

        outputter.WriteLine($"\nFound {this.notDoneIssues.Count} issues that are Required For Go-live but not Done.");

        await UpdateSheet();
    }

    private async Task LoadData()
    {
        outputter.WriteLine($"Retrieving {Constants.Envest} initiatives and their issues...");

        await jiraIssueRepository.GetInitiatives();
        // Get all initiatives and PM plans
        var (initiatives, pmPlans) = await jiraIssueRepository.GetPmPlans(0);

        // Filter for Envest PM Plans that are Required for Go-live
        var envestPmPlans = pmPlans
            .Where(p => p.Customer.Contains(Constants.Envest) && p.RequiredForGoLive)
            .ToList();

        outputter.WriteLine($"Found {envestPmPlans.Count} {Constants.Envest} PM Plans marked as Required For Go-live");

        var notDoneIssues = new List<EnvestNotDoneIssue>();

        // For each PM plan, process its child tickets
        foreach (var pmPlan in envestPmPlans)
        {
            outputter.WriteLine($"Processing PM Plan: {pmPlan.Key} - {pmPlan.Summary}");

            // Find the parent initiative for this PM plan
            var parentInitiative = initiatives
                .FirstOrDefault(i => i.ChildPmPlans.Any(child => child.Key == pmPlan.Key));

            if (parentInitiative == null)
            {
                outputter.WriteLine($"  Warning: Could not find parent initiative for {pmPlan.Key}");
                continue;
            }

            foreach (var ticket in pmPlan.ChildTickets)
            {
                // Cast to BasicJiraTicket to access Status and Summary properties
                if (ticket is BasicJiraTicket basicTicket)
                {
                    // Only include tickets that are not Done
                    if (basicTicket.Status != Constants.DoneStatus)
                    {
                        notDoneIssues.Add(new EnvestNotDoneIssue(
                            parentInitiative.Key,
                            parentInitiative.Summary,
                            basicTicket.Key,
                            basicTicket.Summary,
                            basicTicket.Status,
                            basicTicket.IssueType
                            ));
                    }
                }
            }
        }

        await LoadExtraFields(notDoneIssues);
        this.notDoneIssues = notDoneIssues;
        outputter.WriteLine($"Complete. Found {notDoneIssues.Count} issues not Done.");
    }

    private async Task LoadExtraFields(IList<EnvestNotDoneIssue> issues)
    {
        const int batchSize = 1000;
        var idArray = issues.Select(i => i.IssueKey).Distinct().ToArray();
        var allExtraData = new List<ExtraData>();
        for(var batchIndex = 0; batchIndex < issues.Count; batchIndex += batchSize)
        {
            var thisBatch = string.Join(',', idArray.Skip(batchIndex).Take(batchSize).ToArray());
            var jql = $"key IN ({thisBatch})";
            allExtraData.AddRange((await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(ExtraData.Create));
        }

        this.extraData = allExtraData;
    }

    private async Task UpdateSheet()
    {
        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.ClearRange(SheetTabName, "A2:Z10000");
        await sheetUpdater.ClearRangeFormatting(SheetTabName, 1, 10000, 0, 26);

        var reportData = new List<IList<object?>>();
        var previousHeader = string.Empty;
        var row = 1;
        foreach (var issue in this.notDoneIssues)
        {
            var header = issue.InitiativeKey;
            if (previousHeader != header)
            {
                reportData.Add([JiraUtil.HyperlinkDiscoTicket(header), issue.InitiativeSummary]);
                previousHeader = header;
                await sheetUpdater.BoldCellsFormat(SheetTabName, row, row + 1, 0, 3);
                row++;
            }

            var extraDataRow = this.extraData.First(d => d.Key == issue.IssueKey);
            reportData.Add([string.Empty, JiraUtil.HyperlinkTicket(issue.IssueKey), issue.Summary, issue.Status, extraDataRow.StoryPoints, extraDataRow.Priority, extraDataRow.Exalate]);
            row++;
        }

        sheetUpdater.EditSheet($"{SheetTabName}!A2", reportData, true);

        outputter.WriteLine($"Updated Google Sheet with {this.notDoneIssues.Count} issues: https://docs.google.com/spreadsheets/d/{GoogleSheetId}/edit#gid=0");
        await sheetUpdater.SubmitBatch();
    }

    /// <summary>
    ///     Represents a Jira Issue that is Required For Go-live but not yet Done,
    ///     related to an Envest Initiative.
    /// </summary>
    public record EnvestNotDoneIssue(
        string InitiativeKey,
        string InitiativeSummary,
        string IssueKey,
        string Summary,
        string Status,
        string IssueType);

    public record ExtraData(string Key, double StoryPoints, string Priority, string Exalate)
    {
        public static ExtraData Create(dynamic d)
        {
            return new ExtraData(JiraFields.Key.Parse(d), JiraFields.StoryPoints.Parse(d), JiraFields.Priority.Parse(d), JiraFields.Exalate.Parse(d));
        }
    }
}
