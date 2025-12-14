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
        var newPmPlanList = await ExpandEpicsToIncludeTheirChildren();
        this.pmPlans = newPmPlanList;
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
            var children = issues.Where(i => pmPlan.ChildTickets.Any(cp => cp.Key == i.Key)).ToList();
            if (children.Any())
            {
                var updatedPmPlan = pmPlan with { ChildTickets = children };
                newPmPlanList.Add(updatedPmPlan);
            }
            else
            {
                newPmPlanList.Add(pmPlan);
            }
        }

        this.pmPlans = newPmPlanList;
    }

    private async Task<List<BasicJiraPmPlan>> ExpandEpicsToIncludeTheirChildren()
    {
        var allEpicKeys = this.pmPlans
            .SelectMany(p => p.ChildTickets)
            .Where(x => x.IssueType == Constants.EpicType)
            .Select(x => x.Key)
            .Distinct()
            .OrderBy(x => x);

        var epicChildren = (await runner.GetEpicChildren(allEpicKeys.ToArray())).ToList();
        var newPmPlanList = new List<BasicJiraPmPlan>();
        foreach (var pmPlan in this.pmPlans)
        {
            var newChildrenList = new List<IJiraKeyedIssue>();
            foreach (var child in pmPlan.ChildTickets.Where(cp => cp.IssueType == Constants.EpicType))
            {
                newChildrenList.Add(child);
                var grandChildren = epicChildren.Where(ec => ec.Parent == child.Key);
                newChildrenList.AddRange(grandChildren);
            }

            var newPmPlan = pmPlan with { ChildTickets = newChildrenList };
            newPmPlanList.Add(newPmPlan);
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
