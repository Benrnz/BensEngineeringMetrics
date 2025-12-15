namespace BensEngineeringMetrics.Jira;

public class JiraIssueRepository(IJiraQueryRunner runner) : IJiraIssueRepository
{
    private List<BasicJiraInitiative> initiatives = new();
    private List<BasicJiraPmPlan> pmPlans = new();

    public async Task<IReadOnlyList<BasicJiraInitiative>> OpenInitiatives()
    {
        if (this.initiatives.Any())
        {
            return this.initiatives;
        }

        this.initiatives.AddRange(await runner.GetOpenInitiatives());
        Console.WriteLine($"Retrieved {this.initiatives.Count} initiatives.");

        return this.initiatives;
    }

    public async Task<(IReadOnlyList<BasicJiraInitiative> mappedInitiatives, IReadOnlyList<BasicJiraPmPlan> pmPlans)> OpenPmPlans()
    {
        if (this.pmPlans.Any())
        {
            return (this.initiatives, this.pmPlans);
        }

        this.pmPlans.AddRange(await runner.GetOpenIdeas());
        Console.WriteLine($"Retrieved {this.pmPlans.Count} PmPlan Ideas.");

        await MapPmPlanIdeasToInitiatives();

        return (this.initiatives, this.pmPlans);
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
        Console.WriteLine($"PmPlan grandchildren fetched: {epicChildren.Count} tickets");
        var newPmPlanList = new List<BasicJiraPmPlan>();
        foreach (var pmPlan in this.pmPlans)
        {
            var newChildrenList = new List<IJiraKeyedIssue>(pmPlan.ChildTickets);
            foreach (var child in pmPlan.ChildTickets.Where(cp => cp.IssueType == Constants.EpicType))
            {
                var grandChildren = epicChildren.Where(ec => ec.Parent == child.Key);
                newChildrenList.AddRange(grandChildren);
            }

            var newPmPlan = pmPlan with { ChildTickets = newChildrenList };
            newPmPlanList.Add(newPmPlan);
        }

        return newPmPlanList;
    }

    private async Task MapPmPlanIdeasToInitiatives()
    {
        if (!this.initiatives.Any() || !this.pmPlans.Any())
        {
            return;
        }

        // Loop through all Epics and pull through their children
        var newPmPlanList = await ExpandEpicsToIncludeTheirChildren();
        this.pmPlans = newPmPlanList;

        // Add PmPlan links into the Initiatives.
        var newInitiativeList = MapPmPlanLinksIntoInitiatives();
        this.initiatives = newInitiativeList;
    }

    private List<BasicJiraInitiative> MapPmPlanLinksIntoInitiatives()
    {
        var newInitiativeList = new List<BasicJiraInitiative>();
        foreach (var initiative in this.initiatives)
        {
            var children = this.pmPlans.Where(p => initiative.ChildPmPlans.Any(ip => ip.Key == p.Key)).ToList();
            if (children.Any())
            {
                var updatedInitiative = initiative with { ChildPmPlans = children };
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
