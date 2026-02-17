using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

/// <summary>
///     This task retrieves all Jira Issues related to top-level Jira Initiatives for the customer Envest
///     that are marked as "Required For Go-live" but are not yet Done.
/// </summary>
public class RemainingWorkEnvestTask(IJiraIssueRepository jiraIssueRepository, IOutputter outputter, IWorkSheetUpdater sheetUpdater) : IEngineeringMetricsTask
{
    private const string TaskKey = "REMAINING_WORK_ENVEST";
    private const string GoogleSheetId = "1OVUx08nBaD8uH-klNAzAtxFSKTOvAAk5Vnm11ALN0Zo";
    private const string SheetTabName = "Remaining Work";

    public IList<EnvestNotDoneIssue> NotDoneIssues { get; private set; } = new List<EnvestNotDoneIssue>();

    public string Description => $"Find all {Constants.Envest} Jira Issues Required For Go-live that are not Done";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        outputter.WriteLine($"{Key} - {Description}");

        await LoadData();

        outputter.WriteLine($"\nFound {NotDoneIssues.Count} issues that are Required For Go-live but not Done.");

        await UpdateSheet();
    }

    private async Task LoadData()
    {
        if (NotDoneIssues.Any())
        {
            // Data already loaded
            return;
        }

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

        NotDoneIssues = notDoneIssues;
        outputter.WriteLine($"Complete. Found {notDoneIssues.Count} issues not Done.");
    }

    private async Task UpdateSheet()
    {
        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.ClearRange(SheetTabName);
        await sheetUpdater.ClearRangeFormatting(SheetTabName, 0, 10000, 0, 26);

        var reportData = new List<IList<object?>>();
        var previousHeader = string.Empty;
        var row = 0;
        foreach (var issue in NotDoneIssues)
        {
            var header = issue.InitiativeKey;
            if (previousHeader != header)
            {
                reportData.Add([JiraUtil.HyperlinkDiscoTicket(header), issue.InitiativeSummary]);
                row++;
                previousHeader = header;
                await sheetUpdater.BoldCellsFormat(SheetTabName, row, row + 1, 0, 2);
            }

            reportData.Add([string.Empty, JiraUtil.HyperlinkTicket(issue.IssueKey), issue.Summary, issue.Status]);
            row++;
        }

        sheetUpdater.EditSheet($"{SheetTabName}!A1", reportData, true);

        outputter.WriteLine($"Updated Google Sheet with {NotDoneIssues.Count} issues: https://docs.google.com/spreadsheets/d/{GoogleSheetId}/edit#gid=0");
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
}
