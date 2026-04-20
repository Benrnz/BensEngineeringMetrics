using System.Text;
using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

public class CalculateDailyReportTask(
    ICsvExporter exporter,
    IJiraQueryRunner runner,
    IWorkSheetReader sheetReader,
    IWorkSheetUpdater sheetUpdater,
    IReadableOutputter outputter,
    ISlackClient slack,
    ITeamVelocityRepository velocityReppo)
    : IEngineeringMetricsTask
{
    private const string GoogleSheetId = "1PCZ6APxgEF4WDJaMqLvXDztM47VILEy2RdGDgYiXguQ";
    private const string KeyString = "DAILY";
    private const string DefaultSlackChannel = "Bens-Test-Channel";
    private const double TimeLimitInHours = 8.0;

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.StoryPoints,
        JiraFields.Team,
        JiraFields.Sprint,
        JiraFields.AssigneeDisplay,
        JiraFields.FlagCount,
        JiraFields.IssueType,
        JiraFields.Severity,
        JiraFields.BugType
    ];

    public string Key => KeyString;
    public string Description => "Calculate the _daily_ stats for the daily report for the two teams involved.";

    public async Task ExecuteAsync(string[] args)
    {
        outputter.WriteLine($"{Key} - {Description}");
        await sheetReader.Open(GoogleSheetId);
        outputter.ResetBuffer();
        var updateCounter = 0;

        var myTeams = new[]
        {
            new
            {
                Config = JiraTeamConfig.Teams.Single(t => t.TeamId == Constants.TeamSuperclass),
                Jql = $"Project = {Constants.JavPmJiraProjectKey} AND \"Team[Team]\" = {{0}} AND Sprint IN openSprints()"
            },
            new
            {
                Config = JiraTeamConfig.Teams.Single(t => t.TeamId == Constants.TeamPhantom),
                Jql = $"Project = {Constants.JavPmJiraProjectKey} AND \"Team[Team]\" = {{0}} AND Sprint IN openSprints()"
            },
            new
            {
                Config = JiraTeamConfig.Teams.Single(t => t.TeamId == Constants.TeamWizards),
                Jql = $"Project = {Constants.JavPmJiraProjectKey} AND \"Team[Team]\" = {{0}} AND Sprint IN openSprints()"
            },
            new
            {
                Config = JiraTeamConfig.Teams.Single(t => t.TeamId == Constants.TeamSpearhead),
                Jql = $"Project = {Constants.JavPmJiraProjectKey} AND \"Team[Team]\" = {{0}} AND Sprint IN openSprints()"
            },
            new
            {
                Config = JiraTeamConfig.Teams.Single(t => t.TeamId == Constants.TeamOfficetech),
                Jql = $"Project = {Constants.OtPmJiraProjectKey} AND Sprint IN openSprints()"
            }
        };

        foreach (var team in myTeams)
        {
            if (await CalculateTeamStats(string.Format(team.Jql, team.Config.TeamId), team.Config))
            {
                updateCounter++;
                var message = outputter.ReadTextAndResetBuffer();
                if (!string.IsNullOrWhiteSpace(team.Config.SlackChannel))
                {
                    await slack.SendMessageToChannel(team.Config.SlackChannel, message);
                }

                await slack.SendMessageToChannel(DefaultSlackChannel, message);
                await sheetUpdater.Open(GoogleSheetId);
                sheetUpdater.EditSheet($"{team.Config.TeamName}!H1", [[DateTimeOffset.Now.ToString("o")]], true);
                await sheetUpdater.SubmitBatch();
            }
            else
            {
                // Always output to default channel for every run.
                var message = outputter.ReadTextAndResetBuffer();
                await slack.SendMessageToChannel(DefaultSlackChannel, message);
            }
        }

        outputter.WriteLine($"{updateCounter} updates posted to Slack.");
    }

    private async Task<bool> CalculateTeamStats(string jql, TeamConfig teamConfig)
    {
        var sheetData = await sheetReader.ReadData($"'{teamConfig.TeamName}'!A1:H1000");
        // Check to see if last update was too recent. Avoid spamming slack channels.
        var update = CheckForUpdateRecency(sheetData, teamConfig.TeamName);

        var agileSprint = await runner.GetCurrentSprintForBoard(teamConfig.BoardId);
        if (agileSprint == null)
        {
            outputter.WriteLine($"Unable to pull current sprint for {teamConfig.TeamName}.");
            return false;
        }

        outputter.WriteLine(":javln: Sprint Stats Update :javln:");
        outputter.WriteLine($"{teamConfig.TeamName} Team: '{agileSprint.Name}' Start-date: {agileSprint.StartDate:d-MMM-yy}");
        outputter.Write("``` ");
        var tickets = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(CreateJiraIssue).ToList();
        var totalTickets = tickets.Count();
        var totalStoryPoints = Math.Round(tickets.Sum(t => t.StoryPoints), 1);
        var remainingTickets = tickets.Count(t => t.Status != Constants.DoneStatus);
        var remainingStoryPoints = Math.Round(tickets.Where(t => t.Status != Constants.DoneStatus).Sum(t => t.StoryPoints), 1);
        var ticketsInQa = tickets.Count(t => t.Status == Constants.InQaStatus);
        var ticketsInDev = tickets.Count(t => t.Status == Constants.InDevStatus);
        var ticketsFlagged = tickets.Sum(t => t.FlagCount);
        var p1Bugs = tickets.Count(t => t.Type == Constants.BugType && t is { Severity: Constants.SeverityCritical, BugType: Constants.BugTypeProduction or Constants.BugTypeUat });
        var p2Bugs = tickets.Count(t => t.Type == Constants.BugType && t is { Severity: Constants.SeverityMajor, BugType: Constants.BugTypeProduction or Constants.BugTypeUat });
        var ticketNoEstimate = tickets.Count(t => t.Type is Constants.StoryType or Constants.BugType or Constants.ImprovementType or Constants.SchemaTaskType && t.StoryPoints == 0);
        var teamVelocity = await velocityReppo.LookUpTeamVelocityByName(teamConfig.TeamName);
        var zeroEstimateTickets = new StringBuilder();
        if (ticketNoEstimate > 0)
        {
            zeroEstimateTickets.Append("(");
            foreach (var ticket in tickets.Where(t => t.Type is Constants.StoryType or Constants.BugType or Constants.ImprovementType or Constants.SchemaTaskType && t.StoryPoints == 0))
            {
                zeroEstimateTickets.Append($"{ticket.Key}, ");
            }

            zeroEstimateTickets.Append(").");
        }

        outputter.WriteLine(
            $"    - Total Tickets: {totalTickets}, {remainingTickets} remaining, {totalTickets - remainingTickets} done. ({1 - ((double)remainingTickets / totalTickets):P0} Done). ");
        outputter.WriteLine(
            $"     - Total Story Points: {totalStoryPoints}, {remainingStoryPoints} remaining, {totalStoryPoints - remainingStoryPoints:F1} done. ({1 - (remainingStoryPoints / totalStoryPoints):P0} Done).");
        var velocityMessage = totalStoryPoints > teamVelocity * 1.1 ? "SPRINT IS OVERLOADED" : string.Empty;
        outputter.WriteLine($"Team Velocity is: {teamVelocity}. {velocityMessage}");
        outputter.WriteLine($"     - In Dev: {ticketsInDev}, In QA: {ticketsInQa}");
        if (ticketsFlagged > 0)
        {
            outputter.WriteLine($"     - Flags raised: {ticketsFlagged}");
        }

        if (ticketNoEstimate > 0)
        {
            outputter.WriteLine($"     - Tickets with NO ESTIMATE: {ticketNoEstimate} {zeroEstimateTickets}");
        }

        if (p1Bugs > 0 || p2Bugs > 0)
        {
            outputter.WriteLine($"     - *** P1 Bugs: {p1Bugs}, P2 Bugs: {p2Bugs} ***");
        }

        if (agileSprint.StartDate.ToDateOnly() == DateTime.Today.ToDateOnly())
        {
            await ProcessStartOfSprint(teamConfig.TeamName, agileSprint.StartDate, tickets);
        }
        else
        {
            await ProcessNormalSprintDay(sheetData, teamConfig.TeamName, agileSprint.StartDate, tickets);
        }

        outputter.WriteLine("``` ");

        return update;
    }

    private bool CheckForUpdateRecency(List<List<object>> sheetData, string teamName)
    {
        var headerRow = sheetData.FirstOrDefault();
        if (headerRow is null)
        {
            return true;
        }

        if (headerRow.Count < 8)
        {
            return true;
        }

        if (DateTime.TryParse(headerRow[7].ToString(), out var sheetUpdate))
        {
            return (DateTime.Now - sheetUpdate).TotalHours > TimeLimitInHours;
        }

        return true;
    }

    private JiraIssue CreateJiraIssue(dynamic ticket)
    {
        return new JiraIssue(
            JiraFields.Key.Parse(ticket),
            JiraFields.Status.Parse(ticket),
            JiraFields.StoryPoints.Parse(ticket) ?? 0,
            JiraFields.Team.Parse(ticket),
            JiraFields.AssigneeDisplay.Parse(ticket),
            JiraFields.FlagCount.Parse(ticket),
            JiraFields.IssueType.Parse(ticket),
            JiraFields.Severity.Parse(ticket) ?? string.Empty,
            JiraFields.BugType.Parse(ticket) ?? string.Empty
        );
    }

    private JiraIssue CreateJiraIssue(List<object> sheetData)
    {
        return new JiraIssue(
            sheetData[0].ToString() ?? throw new NotSupportedException("Key"),
            sheetData[1].ToString() ?? throw new NotSupportedException("Status"),
            double.Parse(sheetData[2].ToString() ?? "0"),
            sheetData[3].ToString() ?? throw new NotSupportedException("Team"),
            sheetData[4].ToString() ?? string.Empty,
            int.Parse(sheetData[5].ToString() ?? "0"),
            string.Empty,
            string.Empty
        );
    }

    private async Task ProcessNormalSprintDay(List<List<object>> sheetData, string teamName, DateTimeOffset sprintStart, List<JiraIssue> tickets)
    {
        var headerRow = sheetData.FirstOrDefault();
        if (headerRow is null || headerRow.Count < 7 || !DateTimeOffset.TryParse(headerRow[6].ToString(), out var sheetStart))
        {
            outputter.WriteLine("Cache Sheet appears blank or invalid, assuming start of sprint is today...");
            await ProcessStartOfSprint(teamName, sprintStart, tickets);
            return;
        }

        if (sheetStart.ToDateOnly() != sprintStart.ToDateOnly())
        {
            outputter.WriteLine($"Looks like we're starting a new sprint. Sprint start date {sprintStart:d} doesn't match the date in the cache sheet of {sheetStart:d}.");
            await ProcessStartOfSprint(teamName, sprintStart, tickets);
            return;
        }

        var originalTickets = sheetData.Skip(1).Select(CreateJiraIssue).ToList(); // skip header row

        outputter.WriteLine("Removed tickets since start of sprint:");
        var removedTickets = tickets.Where(t => originalTickets.All(o => o.Key != t.Key)).ToList();
        if (removedTickets.Any())
        {
            var removedTicketsLine = "    " + string.Join("", removedTickets.Select(t => $"{t.Key} ({t.StoryPoints}sp), "));
            outputter.WriteLine(removedTicketsLine);
            outputter.WriteLine($"    {removedTickets.Count} total. {removedTickets.Sum(t => t.StoryPoints):F1}sp total.");
        }
        else
        {
            outputter.WriteLine("    None");
        }

        outputter.WriteLine("New tickets added since start of sprint:");
        var newTickets = originalTickets.Where(o => tickets.All(t => t.Key != o.Key)).ToList();
        if (newTickets.Any())
        {
            var newTicketsLine = "    " + string.Join("", newTickets.Select(t => $"{t.Key} ({t.StoryPoints}sp), "));
            outputter.WriteLine(newTicketsLine);
            outputter.WriteLine($"    {newTickets.Count} total. {newTickets.Sum(t => t.StoryPoints):F1}sp total.");
        }
        else
        {
            outputter.WriteLine("    None");
        }
    }

    private async Task ProcessStartOfSprint(string teamName, DateTimeOffset sprintStart, List<JiraIssue> tickets)
    {
        // Save the list of tickets to Google Drive
        outputter.WriteLine("Resetting sprint story cache...");
        var fileName = $"{Key}_{teamName}";
        exporter.SetFileNameMode(FileNameMode.ExactName, fileName);
        var pathAndFileName = exporter.Export(tickets, () => $"Key,Status,StoryPoints,Team,Assignee,FlagCount,{sprintStart}");
        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.ClearRange($"{teamName}");
        await sheetUpdater.ImportFile($"'{teamName}'!A1", pathAndFileName);
        await sheetUpdater.SubmitBatch();
    }

    private record JiraIssue(
        string Key,
        string Status,
        double StoryPoints,
        string Team,
        string? Assignee,
        int FlagCount,
        string Type,
        string? Severity = "",
        string? BugType = "");
}
