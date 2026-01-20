using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

// ReSharper disable once UnusedType.Global
internal interface IEnvestPmPlanStories
{
    IEnumerable<EnvestPmPlanStories.JiraPmPlan> PmPlans { get; }
    Task<IReadOnlyList<EnvestPmPlanStories.JiraIssueWithPmPlan>> RetrieveAllStoriesMappingToPmPlan(string? additionalCriteria = null);
}

internal class EnvestPmPlanStories(IJiraQueryRunner runner, IOutputter outputter) : IEnvestPmPlanStories
{
    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.StoryPoints,
        JiraFields.OriginalEstimate,
        JiraFields.IssueType,
        JiraFields.ReporterDisplay,
        JiraFields.ParentKey,
        JiraFields.Created
    ];

    private static readonly IFieldMapping[] PmPlanFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IssueType,
        JiraFields.PmPlanHighLevelEstimate,
        JiraFields.EstimationStatus,
        JiraFields.StoryPoints,
        JiraFields.IsReqdForGoLive
    ];

    private IReadOnlyList<JiraIssueWithPmPlan> cachedIssues = [];

    public IEnumerable<JiraPmPlan> PmPlans { get; private set; } = [];

    public async Task<IReadOnlyList<JiraIssueWithPmPlan>> RetrieveAllStoriesMappingToPmPlan(string? additionalCriteria = null)
    {
        if (this.cachedIssues.Any())
        {
            return this.cachedIssues;
        }

        additionalCriteria ??= string.Empty;
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        outputter.WriteLine(jqlPmPlans);
        var childrenJql = $"project=JAVPM AND (issue in (linkedIssues(\"{{0}}\")) OR parent in (linkedIssues(\"{{0}}\"))) {additionalCriteria} ORDER BY key";
        outputter.WriteLine($"ForEach PMPLAN: {childrenJql}");
        PmPlans = (await runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields)).Select(JiraPmPlan.Create);

        var allIssues = new Dictionary<string, JiraIssueWithPmPlan>(); // Ensure the final list of JAVPMs is unique NO DUPLICATES
        foreach (var pmPlan in PmPlans)
        {
            var children = await runner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.Key), Fields);
            outputter.WriteLine($"Fetched {children.Count} children for {pmPlan.Key}");
            foreach (var child in children)
            {
                JiraIssueWithPmPlan issue = JiraIssueWithPmPlan.Create(child, pmPlan);
                allIssues.TryAdd(issue.Key, issue);
            }
        }

        return this.cachedIssues = allIssues.Values.ToList();
    }

    internal record JiraPmPlan(string Key, string Summary, bool IsReqdForGoLive, string EstimationStatus, double PmPlanHighLevelEstimate)
    {
        public static JiraPmPlan Create(dynamic i)
        {
            return new JiraPmPlan(
                JiraFields.Key.Parse(i),
                JiraFields.Summary.Parse(i),
                JiraFields.IsReqdForGoLive.Parse(i),
                JiraFields.EstimationStatus.Parse(i) ?? string.Empty,
                JiraFields.PmPlanHighLevelEstimate.Parse(i) ?? 0.0);
        }
    }

    internal record JiraIssueWithPmPlan(
        string PmPlan,
        string Key,
        string Summary,
        string Status,
        string Type,
        double StoryPoints,
        bool IsReqdForGoLive,
        string? EstimationStatus,
        double PmPlanHighLevelEstimate,
        DateTimeOffset CreatedDateTime,
        string PmPlanSummary,
        string? ParentEpic = null)
    {
        public static JiraIssueWithPmPlan Create(dynamic i, JiraPmPlan pmPlan)
        {
            var storyPointsField = JiraFields.StoryPoints.Parse(i) ?? 0.0;

            var typedIssue = new JiraIssueWithPmPlan(
                pmPlan.Key,
                JiraFields.Key.Parse(i),
                JiraFields.Summary.Parse(i),
                JiraFields.Status.Parse(i),
                JiraFields.IssueType.Parse(i),
                storyPointsField,
                pmPlan.IsReqdForGoLive,
                pmPlan.EstimationStatus,
                pmPlan.PmPlanHighLevelEstimate,
                JiraFields.Created.Parse(i),
                pmPlan.Summary,
                JiraFields.ParentKey.Parse(i));
            return typedIssue;
        }
    }
}
