using System.Text.RegularExpressions;
using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

public class IncidentDashboard(
    IJiraQueryRunner runner,
    IWorkSheetUpdater sheetUpdater,
    ISlackClient slack,
    IGreenHopperClient greenHopperClient,
    IOutputter outputter,
    ICsvExporter exporter)
    : IEngineeringMetricsTask
{
    private const string TaskKey = "INCIDENTS";
    private const string JavPmGoogleSheetId = "16bZeQEPobWcpsD8w7cI2ftdSoT1xWJS8eu41JTJP-oI";
    private const string OtPmGoogleSheetId = "14Dqa1UVXQJrAViBHgbS8kHBmHi61HnkZAKa6wCsTL2E";
    private const string GoogleSheetTabName = "Open Incidents Dashboard";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Status,
        JiraFields.Summary,
        JiraFields.Team,
        JiraFields.StoryPoints,
        JiraFields.CustomersMultiSelect,
        JiraFields.Sprint,
        JiraFields.Severity,
        JiraFields.UpdatedDate
    ];

    private readonly IList<SlackChannelSummary> incidentSlackChannels = new List<SlackChannelSummary>();

    private string integrationTeam = string.Empty;
    private string rubyDucksTeam = string.Empty;

    private List<IList<object?>> sheetData = new();
    private string spearheadTeam = string.Empty;
    private string superclassTeam = string.Empty;

    public string Description => "Pulls data from Jira and Slack to give a combined view of all open incidents.";
    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        this.spearheadTeam = JiraTeamConfig.Teams.Single(t => t.TeamId == Constants.TeamSpearhead).JiraName;
        this.rubyDucksTeam = JiraTeamConfig.Teams.Single(t => t.TeamId == Constants.TeamRubyDucks).JiraName;
        this.superclassTeam = JiraTeamConfig.Teams.Single(t => t.TeamId == Constants.TeamSuperclass).JiraName;
        this.integrationTeam = JiraTeamConfig.Teams.Single(t => t.TeamId == Constants.TeamIntegration).JiraName;

        outputter.WriteLine("Updating Incident Dashboard for JAVPM...");
        await BuildAllTablesForProject(Constants.JavPmJiraProjectKey, JavPmGoogleSheetId);
        outputter.WriteLine("Updating Incident Dashboard for OTPM...");
        await BuildAllTablesForProject(Constants.OtPmJiraProjectKey, OtPmGoogleSheetId);
    }

    private async Task BuildAllTablesForProject(string project, string sheetId)
    {
        this.sheetData = new List<IList<object?>>();
        await sheetUpdater.Open(sheetId);
        sheetUpdater.ClearRange(GoogleSheetTabName, "A2:Z10000");
        await sheetUpdater.ClearRangeFormatting(GoogleSheetTabName, 1, 10000, 0, 26);

        SetLastUpdateTime();
        var jiraIssues = await RetrieveJiraData(project);
        var activeSprint = await RetrieveActiveSprintTickets(project);
        CreateTableForOpenTicketSummary(jiraIssues, activeSprint);
        await TeamVelocityTable(project);
        await CreateTableForSlackChannels();
        CreateTableForPriorityBugList(jiraIssues, Constants.SeverityCritical);
        CreateTableForPriorityBugList(jiraIssues, Constants.SeverityMajor);

        sheetUpdater.EditSheet($"{GoogleSheetTabName}!A1", this.sheetData, true);
        await sheetUpdater.SubmitBatch();
    }

    private void CreateTableForOpenTicketSummary(IReadOnlyList<JiraIssue> jiraIssues, IReadOnlyList<JiraIssue> activeSprint)
    {
        outputter.WriteLine("Creating table for open ticket summary...");

        // Row 1 - Headings Row
        this.sheetData.Add([null, "Number of P1s", "Number of P2s", "Spearhead", "Superclass", "Ruby Ducks", "Integration"]);
        sheetUpdater.BoldCellsFormat(GoogleSheetTabName, this.sheetData.Count - 1, this.sheetData.Count, 0, 7);

        // Row 2 - Active Sprint tickets row
        this.sheetData.Add([
            "Total Open Tickets:",
            jiraIssues.Count(i => i.Severity == Constants.SeverityCritical),
            jiraIssues.Count(i => i.Severity == Constants.SeverityMajor),
            jiraIssues.Count(i => i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.spearheadTeam),
            jiraIssues.Count(i => i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.superclassTeam),
            jiraIssues.Count(i => i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.rubyDucksTeam),
            jiraIssues.Count(i => i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.integrationTeam)
        ]);
        sheetUpdater.BoldCellsFormat(GoogleSheetTabName, this.sheetData.Count - 1, this.sheetData.Count, 0, 1);

        // Row 3 -
        this.sheetData.Add([
            "In Sprint:",
            activeSprint.Count(i => i.Severity == Constants.SeverityCritical && !string.IsNullOrWhiteSpace(i.Sprint) && i.Sprint != Constants.NoSprint),
            activeSprint.Count(i => i.Severity == Constants.SeverityMajor && !string.IsNullOrWhiteSpace(i.Sprint) && i.Sprint != Constants.NoSprint),
            activeSprint.Count(i =>
                i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.spearheadTeam && !string.IsNullOrWhiteSpace(i.Sprint) && i.Sprint != Constants.NoSprint),
            activeSprint.Count(i =>
                i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.superclassTeam && !string.IsNullOrWhiteSpace(i.Sprint) && i.Sprint != Constants.NoSprint),
            activeSprint.Count(i =>
                i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.rubyDucksTeam && !string.IsNullOrWhiteSpace(i.Sprint) && i.Sprint != Constants.NoSprint),
            activeSprint.Count(i =>
                i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.integrationTeam && !string.IsNullOrWhiteSpace(i.Sprint) && i.Sprint != Constants.NoSprint)
        ]);

        // Group by customer
        var customerTickets = new List<CustomerTickets>();
        foreach (var customer in GetUniqueCustomerList(jiraIssues).Where(c => c != string.Empty))
        {
            var group = jiraIssues.Where(i => i.CustomerArray.Contains(customer)).ToList();

            var p1Issues = group.Count(i => i.Severity == Constants.SeverityCritical);
            var p2Issues = group.Count(i => i.Severity == Constants.SeverityMajor);
            var spearheadIssues = group.Count(i => i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.spearheadTeam);
            var superclassIssues = group.Count(i => i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.superclassTeam);
            var rubyDucksIssues = group.Count(i => i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.rubyDucksTeam);
            var integrationIssues = group.Count(i => i.Severity is Constants.SeverityCritical or Constants.SeverityMajor && i.Team == this.integrationTeam);
            if (p1Issues == 0 && p2Issues == 0)
            {
                continue;
            }

            customerTickets.Add(new CustomerTickets(customer, p1Issues, p2Issues, spearheadIssues, superclassIssues, rubyDucksIssues, integrationIssues));
        }

        // Row 4+
        var rank = 1;
        foreach (var customer in customerTickets.OrderByDescending(c => c.P1Count).ThenByDescending(c => c.P2Count))
        {
            this.sheetData.Add([
                $"{rank++}) {customer.CustomerName}", customer.P1Count, customer.P2Count, customer.SpearheadCount, customer.SuperclassCount, customer.RubyDucksCount, customer.IntegrationCount
            ]);
            if (rank > 5)
            {
                break;
            }
        }

        this.sheetData.Add([]);
        this.sheetData.Add([]);
    }

    private void CreateTableForPriorityBugList(IReadOnlyList<JiraIssue> jiraIssues, string severity)
    {
        var priorityName = severity == Constants.SeverityCritical ? "P1" : "P2";
        outputter.WriteLine($"Creating table for {priorityName} list...");

        this.sheetData.Add([$"List of Open {priorityName}s", "Status", "Customer", "Summary", "Sprint", "Last Activity (days ago)"]);
        sheetUpdater.BoldCellsFormat(GoogleSheetTabName, this.sheetData.Count - 1, this.sheetData.Count, 0, 6);
        foreach (var issue in jiraIssues.Where(i => !i.Customers.Contains(Constants.Javln) && i.Severity == severity).OrderByDescending(i => i.LastActivity))
        {
            this.sheetData.Add([
                JiraUtil.HyperlinkTicket(issue.Key),
                issue.Status,
                issue.Customers,
                issue.Summary,
                issue.Sprint,
                issue.LastActivity
            ]);
        }

        this.sheetData.Add([]);
        this.sheetData.Add([]);
    }

    private async Task CreateTableForSlackChannels()
    {
        outputter.WriteLine("Creating table for Slack Channel Incidents...");
        if (!this.incidentSlackChannels.Any())
        {
            var channels = await slack.FindAllChannels("incident-");
            var ticketRegex = new Regex(@"\b(JAVPM|OTPM)-\d+\b", RegexOptions.IgnoreCase);
            foreach (var channel in channels)
            {
                var daysAgo = channel.LastMessageTimestamp.HasValue
                    ? (DateTimeOffset.Now - channel.LastMessageTimestamp.Value).TotalDays
                    : 0;
                string? status = null;
                string? ticketKey = null;
                var match = ticketRegex.Match(channel.Name);
                if (match.Success)
                {
                    ticketKey = match.Value.ToUpperInvariant();
                }

                this.incidentSlackChannels.Add(new SlackChannelSummary
                (
                    channel.Name,
                    Age: Math.Round(daysAgo, 1),
                    Link: $"=HYPERLINK(\"https://javln.slack.com/archives/{channel.Id}\", \"{channel.Name}\")",
                    Status: status ?? Constants.Unknown,
                    JiraKey: ticketKey ?? Constants.Unknown
                ));
            }

            var jiraKeys = string.Join(", ", this.incidentSlackChannels
                .Where(c => c.JiraKey != Constants.Unknown)
                .Select(c => c.JiraKey)
                .ToArray());

            var jiras = (await runner.SearchJiraIssuesWithJqlAsync($"key IN ({jiraKeys})", [JiraFields.Status]))
                .Select(d => new
                {
                    Key = (string)JiraFields.Key.Parse(d),
                    Status = (string)JiraFields.Status.Parse(d)
                })
                .ToList();

            foreach (var channel in this.incidentSlackChannels.Where(c => c.JiraKey != Constants.Unknown))
            {
                var matchingJira = jiras.FirstOrDefault(j => j.Key == channel.JiraKey);
                channel.Status = matchingJira?.Status ?? Constants.Unknown;
            }
        }

        this.sheetData.Add(["Open Slack Incident-* Channels", null, "Last Message (days ago)", "Status"]);
        await sheetUpdater.BoldCellsFormat(GoogleSheetTabName, this.sheetData.Count - 1, this.sheetData.Count, 0, 4);
        this.sheetData.Add([$"{this.incidentSlackChannels.Count} Incident channels open", null, null, null]);

        foreach (var channel in this.incidentSlackChannels.OrderByDescending(c => c.Age))
        {
            this.sheetData.Add([channel.Link, null, channel.Age, channel.Status]);
        }

        this.sheetData.Add([]);
        this.sheetData.Add([]);
    }


    private IOrderedEnumerable<string> GetUniqueCustomerList(IReadOnlyList<JiraIssue> jiraIssues)
    {
        return jiraIssues
            .SelectMany(i => i.CustomerArray)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c);
    }

    private async Task<IReadOnlyList<JiraIssue>> RetrieveActiveSprintTickets(string project)
    {
        var jql = $"""
                   project = "{project}"
                   AND issueType = Bug
                   AND sprint in openSprints()
                   AND ("Customer/s (Multi Select)[Select List (multiple choices)]" != JAVLN OR "Customer/s (Multi Select)[Select List (multiple choices)]" IS EMPTY)
                   """;
        var issues = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(JiraIssue.CreateJiraIssue).ToList();

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}");
        exporter.Export(issues);

        return issues;
    }

    private async Task<IReadOnlyList<JiraIssue>> RetrieveJiraData(string project)
    {
        var jql = $"""
                   project = "{project}"
                   AND issueType = Bug
                   AND status != Done
                   AND ("Customer/s (Multi Select)[Select List (multiple choices)]" != JAVLN OR "Customer/s (Multi Select)[Select List (multiple choices)]" IS EMPTY)
                   """;
        var issues = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(JiraIssue.CreateJiraIssue).ToList();

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}");
        exporter.Export(issues);

        return issues;
    }

    private void SetLastUpdateTime()
    {
        var row = new List<object?> { null, DateTime.Now.ToString("d-MMM-yy HH:mm") };
        this.sheetData.Add(row);
        this.sheetData.Add(new List<object?>());
    }

    private async Task TeamVelocityTable(string project)
    {
        outputter.WriteLine("Creating table for team velocity...");
        this.sheetData.Add([
            "Team Velocity (Avg Last 5 sprints)", "P1s Defects Avg", "% of work done (based on SP)", "P2s Defects Avg", "% of work done (based on SP)", "Other Defects Avg", "% of work done"
        ]);
        await sheetUpdater.BoldCellsFormat(GoogleSheetTabName, this.sheetData.Count - 1, this.sheetData.Count, 0, 7);

        var teamData = await new TeamVelocityCalculator(runner, greenHopperClient).TeamVelocityTableGetTeamData(project);

        this.sheetData.AddRange(teamData
            .Select(t => (IList<object?>)new List<object?> { t.TeamName, t.AvgP1sClosed, t.P1StoryPointRatio, t.AvgP2sClosed, t.P2StoryPointRatio, t.AvgOtherBugsClosed, t.OtherBugStoryPointRatio }));

        // % format for P1 Capacity
        await sheetUpdater.PercentFormat(
            GoogleSheetTabName,
            this.sheetData.Count - teamData.Count - 1,
            this.sheetData.Count,
            2,
            3);

        // % format for P2 Capacity
        await sheetUpdater.PercentFormat(
            GoogleSheetTabName,
            this.sheetData.Count - teamData.Count - 1,
            this.sheetData.Count,
            4,
            5);

        // % format for Other Capacity
        await sheetUpdater.PercentFormat(
            GoogleSheetTabName,
            this.sheetData.Count - teamData.Count - 1,
            this.sheetData.Count,
            6,
            7);

        this.sheetData.Add([]);
        this.sheetData.Add([]);
    }

    private record JiraIssue(
        string Key,
        string Summary,
        string Sprint,
        string Customers,
        string[] CustomerArray,
        string Severity,
        string Team,
        string Status,
        double LastActivity)
    {
        public static JiraIssue CreateJiraIssue(dynamic d)
        {
            var customer = JiraFields.CustomersMultiSelect.Parse(d) ?? string.Empty;
            var lastUpdatedDate = (DateTimeOffset?)JiraFields.UpdatedDate.Parse(d) ?? DateTimeOffset.MaxValue;
            var lastUpdatedDaysAgo = (DateTimeOffset.Now - lastUpdatedDate).TotalDays;
            var sprint = (string)JiraFields.Sprint.Parse(d);
            return new JiraIssue(
                Key: JiraFields.Key.Parse(d),
                Summary: JiraFields.Summary.Parse(d),
                Sprint: string.IsNullOrWhiteSpace(sprint) ? Constants.NoSprint : sprint,
                Customers: customer,
                CustomerArray: customer.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                Severity: JiraFields.Severity.Parse(d) ?? string.Empty,
                Team: JiraFields.Team.Parse(d) ?? "No Team",
                Status: JiraFields.Status.Parse(d),
                LastActivity: lastUpdatedDaysAgo < 0 ? 999 : Math.Round(lastUpdatedDaysAgo, 1)
            );
        }
    }

    private record CustomerTickets(
        string CustomerName,
        int P1Count,
        int P2Count,
        int SpearheadCount,
        int SuperclassCount,
        int RubyDucksCount,
        int IntegrationCount);

    private record SlackChannelSummary(string Name, string Link, double Age, string JiraKey, string Status)
    {
        public string Status { get; set; } = Status;
    }
}
