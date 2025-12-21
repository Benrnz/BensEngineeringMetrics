using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

/// <summary>
///     See https://javlnsupport.atlassian.net/wiki/spaces/DEVELOPMEN/pages/1284243457/Engineering+Tasks+Summary+-+November+2025
/// </summary>
public class ExportEngineeringTaskAnalysis(IJiraQueryRunner runner, IWorkSheetUpdater sheetUpdater, ICsvExporter exporter, IJiraIssueRepository jiraRepo) : IEngineeringMetricsTask
{
    private const string GoogleSheetIdBms = "1_ANmhfs-kjyCwntbeS2lM0YYOpeBgCDoasZz5UZpl2g";
    private const string GoogleSheetIdOfficetech = "1stl16wfDseznJn8JxfPabF19V7YK4WMYldZoEzD_wwk";
    private const string TaskKey = "ENG_TASK_ANALYSIS";
    private const string PiechartSheetTab = "Latest Piecharts";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.IssueType,
        JiraFields.Summary,
        JiraFields.Created,
        JiraFields.UpdatedDate,
        JiraFields.StoryPoints,
        JiraFields.Team,
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

        var loop = false;
        do
        {
            loop = ParseArguments(args, loop);
            if (this.startDate == DateTimeOffset.MinValue || this.endDate == DateTimeOffset.MinValue)
            {
                // User asked to cancel
                return;
            }

            await RunReportForProduct($"{Constants.JavPmJiraProjectKey},{Constants.EngJiraProjectKey}", GoogleSheetIdBms);
            await RunReportForProduct($"{Constants.OtPmJiraProjectKey},{Constants.OtDoJiraProjectKey}", GoogleSheetIdOfficetech);
        } while (loop);
    }

    private void CreateEngineeringExcellencePieChartData()
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
        InsertEngineeringExcellenceTable(totalCount, totalPoints);
        InsertTicketTypeAnalysisTable(totalCount, totalPoints);
    }

    private async Task CreatePmPlanPieChartData()
    {
        await jiraRepo.GetInitiatives();
        var (openInitiatives, _) = await jiraRepo.GetPmPlans();

        var listOfInterest = MapJiraIssuesToPmPlans(openInitiatives);
        var groupedByInitiative = listOfInterest.GroupBy(x => x.Initiative)
            .Select(g => new { Initiative = g.Key, TicketCount = g.Count(), StoryPointTotal = g.Sum(x => x.StoryPoints) })
            .OrderByDescending(x => x.StoryPointTotal);
        var chartData = new List<IList<object?>>
        {
            new List<object?> { "PMPLAN Initiative", null, "Total Story Points", "Ticket Count" }
        };
        foreach (var group in groupedByInitiative)
        {
            Console.WriteLine($"{group.Initiative}  Tickets:{group.TicketCount} StoryPointTotal:{group.StoryPointTotal}");
            chartData.Add([group.Initiative, null, group.StoryPointTotal, group.TicketCount]);
        }

        sheetUpdater.EditSheet($"'{PiechartSheetTab}'!A50", chartData, true);
        await sheetUpdater.BoldCellsFormat(PiechartSheetTab, 49, 50, 0, 4);
    }

    private async Task ExportIssueData(string monthName, string dataSheetName)
    {
        // Insert PMPLAN Initiative into the issues data
        var map = jiraRepo.LeafTicketToInitiativeMap();
        var newListOfIssues = new List<JiraIssue>();
        foreach (var issue in this.issues)
        {
            var newRecord = issue with { PmPlanInitiativeKey = map.GetValueOrDefault(issue.Key, string.Empty) };
            newListOfIssues.Add(newRecord);
        }

        exporter.SetFileNameMode(FileNameMode.ExactName, $"{Key}_{monthName}_Issues");
        var fileName = exporter.Export(newListOfIssues);
        await sheetUpdater.ImportFile($"{dataSheetName}!A1", fileName);
    }

    private DateTimeOffset GetDateFromConsole(string prompt, bool loop, string commandLineArg)
    {
        do
        {
            string? dateProvided;
            if (loop)
            {
                Console.WriteLine($"Enter a {prompt}. Or enter to exit.");
                dateProvided = Console.ReadLine();
            }
            else
            {
                dateProvided = commandLineArg;
            }

            if (string.IsNullOrEmpty(dateProvided) || dateProvided == "exit")
            {
                return DateTimeOffset.MinValue;
            }

            if (DateTimeOffset.TryParse(dateProvided, out var result))
            {
                return result;
            }

            Console.WriteLine($"ERROR: Invalid start date provided: {dateProvided}");
        } while (true);
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

        sheetUpdater.EditSheet($"'{PiechartSheetTab}'!A3", chartData, true);
        sheetUpdater.BoldCellsFormat(PiechartSheetTab, 2, 3, 0, 4);
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

        sheetUpdater.EditSheet($"'{PiechartSheetTab}'!A26", chartData, true);
        sheetUpdater.BoldCellsFormat(PiechartSheetTab, 25, 26, 0, 4);
        sheetUpdater.BoldCellsFormat(PiechartSheetTab, chartData.Count + 25 - 1, chartData.Count + 25, 0, 4);
    }

    private IList<JiraMonthTicketWithInitiative> MapJiraIssuesToPmPlans(IReadOnlyList<BasicJiraInitiative> initiatives)
    {
        if (!initiatives.Any() || !this.issues.Any())
        {
            return new List<JiraMonthTicketWithInitiative>();
        }

        // flatten initiatives structure into a single list where each leaf ticket is an instance of BasicJiraTicketWithParent, and the Parent field is set to the top level Initiative.
        var result = new List<JiraMonthTicketWithInitiative>();

        foreach (var initiative in initiatives)
        {
            foreach (var childPmPlan in initiative.ChildPmPlans)
            {
                // ChildPmPlans should be BasicJiraPmPlan instances
                if (childPmPlan is not BasicJiraPmPlan pmPlan)
                {
                    continue;
                }

                foreach (var childTicket in pmPlan.ChildTickets)
                {
                    // ChildTickets should be BasicJiraTicket instances and they must be in the set of interest provided above
                    if (childTicket is BasicJiraTicket ticket)
                    {
                        var monthTicket = this.issues.FirstOrDefault(i => i.Key == childTicket.Key);
                        if (monthTicket is not null)
                        {
                            var summaryTicket = new JiraMonthTicketWithInitiative(
                                ticket.Key,
                                $"{initiative.Key} {initiative.Summary}",
                                monthTicket.StoryPoints
                            );
                            result.Add(summaryTicket);
                        }
                    }
                }
            }
        }

        return result;
    }

    private bool ParseArguments(string[] args, bool loop)
    {
        if (args.Length <= 1)
        {
            this.startDate = DateUtils.StartOfMonth(DateTimeOffset.Now);
            this.endDate = DateUtils.EndOfMonth(this.startDate);
            return false;
        }

        if (args.Length < 3)
        {
            Console.WriteLine("ERROR: Only one date was provided, if providing dates please provide both start and end dates in format dd-MM-yyyy");
            throw new ArgumentException("ERROR: Only one date was provided, if providing dates please provide both start and end dates in format dd-MM-yyyy");
        }

        this.endDate = this.startDate = DateTimeOffset.MinValue;
        this.startDate = GetDateFromConsole("start date (inclusive)", loop, args[1]);
        if (this.startDate == DateTimeOffset.MinValue)
        {
            return false;
        }

        this.endDate = GetDateFromConsole("end date (exclusive)", loop, args[2]);
        if (this.endDate == DateTimeOffset.MinValue)
        {
            return false;
        }

        return true;
    }

    private async Task RunReportForProduct(string productKeys, string googleSheetId)
    {
        await sheetUpdater.Open(googleSheetId);
        var monthName = this.startDate.ToString("MMM");
        var dataSheetName = $"{monthName} JiraIssues";
        if (await sheetUpdater.DoesSheetExist(dataSheetName))
        {
            sheetUpdater.ClearRange(dataSheetName);
        }
        else
        {
            // Ensure sheet is created first.
            sheetUpdater.AddSheet(dataSheetName);
            await sheetUpdater.SubmitBatch();
        }

        await sheetUpdater.Open(googleSheetId);

        var jql =
            $"project in ({productKeys}) and type != EPIC AND statusCategoryChangedDate >= '{this.startDate:yyyy-MM-dd}' and statusCategory = Done AND statusCategoryChangedDate < '{this.endDate:yyyy-MM-dd}'";
        Console.WriteLine(jql);
        this.issues = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(JiraIssue.CreateJiraIssue).ToList();

        CreateEngineeringExcellencePieChartData();
        await CreatePmPlanPieChartData();

        await ExportIssueData(monthName, dataSheetName);

        sheetUpdater.EditSheet("Info!B1", [[DateTime.Now.ToString("G")]]);
        await sheetUpdater.SubmitBatch();
    }

    private record JiraMonthTicketWithInitiative(string Key, string Initiative, double StoryPoints);

    private record EngineeringTicketTypeChart(string TicketType, double Percentage, int Count, double StoryPointsPercentage);

    private record JiraIssue(
        string Key,
        string Summary,
        string IssueType,
        DateTimeOffset Created,
        DateTimeOffset UpdatedDate,
        double StoryPoints,
        string Team,
        string WorkDoneBy,
        string[] Labels,
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
                JiraFields.UpdatedDate.Parse(d),
                JiraFields.StoryPoints.Parse(d),
                JiraFields.Team.Parse(d),
                JiraFields.WorkDoneBy.Parse(d),
                labels.Split(','));
        }
    }
}
