namespace BensEngineeringMetrics.Jira;

public class JiraIssueRepository(IJiraQueryRunner runner) : IJiraIssueRepository
{
    private static readonly IFieldMapping[] InitiativeFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IsReqdForGoLive
    ];

    private List<BasicJiraInitiative> initiatives = new();
    private List<BasicJiraPmPlan> pmPlans = new();

    public async Task<IReadOnlyList<BasicJiraInitiative>> OpenInitiatives()
    {
        if (this.initiatives.Any())
        {
            return this.initiatives;
        }

        this.initiatives.AddRange(await runner.GetOpenInitiatives());

        return this.initiatives;
    }

    public async Task<IReadOnlyList<BasicJiraPmPlan>> OpenPmPlans()
    {
        if (this.pmPlans.Any())
        {
            return this.pmPlans;
        }

        this.pmPlans.AddRange(await runner.GetOpenIdeas());

        await MapPmPlanIdeasToInitiatives();

        return this.pmPlans;
    }

    public async Task MapPmPlanIdeasToInitiatives()
    {
        if (!this.initiatives.Any() || !this.pmPlans.Any())
        {
            return;
        }

        // Add PmPlan links into the Initiatives.
        var newInitiativeList = MapPmPlanLinksIntoInitiatives();
        this.initiatives = newInitiativeList;

        // Loop through all Epics and pull through their children
        await ExpandEpicsToIncludeTheirChildren();
    }

    public void MapJiraIssuesToPmPlans(IReadOnlyList<IJiraKeyedIssue> issues)
    {
        if (!this.pmPlans.Any() || !issues.Any())
        {
            return;
        }

        var newPmPlanList = new List<BasicJiraPmPlan>();
        foreach (var pmPlan in this.pmPlans)
        {
            var children = issues.Where(i => pmPlan.ChildrenTicketKeys.Any(cp => cp.Key == i.Key)).ToList();
            if (children.Any())
            {
                var updatedPmPlan = pmPlan with { ChildrenTickets = children };
                newPmPlanList.Add(updatedPmPlan);
            }
            else
            {
                newPmPlanList.Add(pmPlan);
            }
        }

        this.pmPlans = newPmPlanList;
    }

    private async Task<IReadOnlyList<BasicJiraPmPlan>> ExpandEpicsToIncludeTheirChildren()
    {
        var allEpicKeys = this.pmPlans
            .SelectMany(p => p.ChildrenTicketKeys)
            .Where(x => x.IssueType == Constants.EpicType)
            .Select(x => x.Key)
            .Distinct()
            .OrderBy(x => x);

        var jql = $"key IN ({string.Join(',', allEpicKeys)})";
        if (jql.Length < 10)
        {
            return this.pmPlans;
        }

        var epics = await runner.SearchJiraIssuesWithJqlAsync(jql, [JiraFields.IssueType]);
        var newPmPlanList = new List<BasicJiraPmPlan>();
        foreach (var pmPlan in this.pmPlans)
        {
            var newChildrenList = new List<IJiraKeyedIssue>();
            foreach (var child in pmPlan.ChildrenTicketKeys)
            {
                var match = epics.FirstOrDefault(e => e.Key == child.Key);
                if (match is null)
                {
                    newChildrenList.Add(child);
                }
                else
                {
                    // TODO This wont work. The tickets to add will be buried in the issueLinks.
                    newChildrenList.Add(new BasicJiraTicket(match.Key, match.Type));
                }
            }

            newPmPlanList.Add(pmPlan with { ChildrenTicketKeys = xxx, ChildrenTickets = newChildrenList });
        }

        return newPmPlanList;
    }

    private List<BasicJiraInitiative> MapPmPlanLinksIntoInitiatives()
    {
        var newInitiativeList = new List<BasicJiraInitiative>();
        foreach (var initiative in this.initiatives)
        {
            var children = this.pmPlans.Where(p => initiative.PmPlanKeys.Any(ip => ip.Key == p.Key)).ToList();
            if (children.Any())
            {
                var updatedInitiative = initiative with { PmPlanIdeas = children };
                newInitiativeList.Add(updatedInitiative);
            }
            else
            {
                newInitiativeList.Add(initiative);
            }
        }

        return newInitiativeList;
    }
}
