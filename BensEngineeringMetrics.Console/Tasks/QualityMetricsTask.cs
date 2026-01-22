using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

/// <summary>
///     https://javlnsupport.atlassian.net/wiki/spaces/DEVELOPMEN/pages/330924042/Quality+Metrics
/// </summary>
public class QualityMetricsTask(IJiraQueryRunner runner) : IEngineeringMetricsTask
{
    private const string GoogleSheetIdBms = "xxx";
    private const string TaskKey = "QUALITY_METRICS";

    private record ChartMetaData(string TabPrefix, string[] Filters, IFieldMapping[] Fields);

    private List<ChartMetaData> chartsMetaData =
    [
        new("Unresolved vs Resolved bugs",
            ["[METRIC] JAVPM - Total New Or Valid Bugs", "[METRIC] JAVPM - Features Dev Completed"],
            [
                JiraFields.IssueType,
                JiraFields.Status,
                JiraFields.Resolution,
                JiraFields.Resolved
            ]),
        new("Bugs from recent dev",
            [
                "[METRIC] JAVPM - Bug Fix - Code Fix - Bug from recent development - All",
                "[METRIC] JAVPM - Bug Fix - Code Fix - Bug from recent development - Production",
                "[METRIC] JAVPM - Bug Fix - Other bug fixes"
            ],
            [
                JiraFields.IssueType,
                JiraFields.Status,
                JiraFields.Resolution,
                JiraFields.Resolved
            ])
    ];

    public string Description => "Export to a Google Sheet series of graphs for established quality metrics.";
    public string Key => TaskKey;

    public  Task ExecuteAsync(string[] args)
    {
        Console.WriteLine($"{Key} - {Description}");
        Console.WriteLine();

        // TODO
    }

    private record JiraIssue(string Key, string IssueType, string Status, string Resolution, DateTimeOffset Resolved)
    {
        public static JiraIssue Create(dynamic d)
        {
            return new JiraIssue(JiraFields.Key.Parse(d), JiraFields.IssueType.Parse(d), JiraFields.Status.Parse(d), JiraFields.Resolution.Parse(d), JiraFields.Resolved.Parse(d));
        }
    }
}
