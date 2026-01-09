namespace BensEngineeringMetrics.Jira;

internal class JiraIssueRepository(IJiraQueryRunner runner) : IJiraIssueRepository
{
    private List<BasicJiraInitiative> initiatives = new();
    private List<BasicJiraPmPlan> pmPlans = new();

    private IReadOnlyDictionary<string, string> ticketToInitiativeMap = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, string> ticketToPmPlanMap = new Dictionary<string, string>();

    public (string? initiativeKey, IJiraKeyedIssue? foundTicket) FindTicketByKey(string key)
    {
        // Even though it's possible to use the ticketToInitiativeMap here, I am intentionally looping through the whole collection here as I want to
        // avoid confusing and difficult to debug LINQ queries. Using this primarily for testing purposes.
        if (!this.initiatives.Any() || !this.pmPlans.Any())
        {
            return (string.Empty, null);
        }

        foreach (var initiative in this.initiatives)
        {
            foreach (var pmPlan in initiative.ChildPmPlans.OfType<BasicJiraPmPlan>())
            {
                var ticket = pmPlan.ChildTickets.FirstOrDefault(t => t.Key == key);
                if (ticket is not null)
                {
                    return (initiative.Key, ticket);
                }
            }
        }

        return (string.Empty, null);
    }

    public async Task<IReadOnlyList<BasicJiraInitiative>> OpenInitiatives()
    {
        if (this.initiatives.Any())
        {
            return this.initiatives;
        }

        this.initiatives.AddRange(await runner.GetOpenInitiatives());
        Console.WriteLine($"Retrieved {this.initiatives.Count} initiatives.");

        // Clear cached mappings since initiatives changed
        this.ticketToInitiativeMap = new Dictionary<string, string>();
        this.ticketToPmPlanMap = new Dictionary<string, string>();

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

    public IReadOnlyDictionary<string, string> LeafTicketToInitiativeMap()
    {
        if (!this.pmPlans.Any())
        {
            return new Dictionary<string, string>();
        }

        if (this.ticketToInitiativeMap.Any())
        {
            return this.ticketToInitiativeMap;
        }

        this.ticketToInitiativeMap = this.initiatives
            .SelectMany(i => i.ChildPmPlans.OfType<BasicJiraPmPlan>() ?? [], (i, p) => new { InitiativeKey = i.Key, PmPlan = p })
            .SelectMany(x => x.PmPlan.ChildTickets.OfType<BasicJiraTicket>() ?? [], (x, t) => new { x.InitiativeKey, TicketKey = t.Key })
            .GroupBy(x => x.TicketKey)
            .ToDictionary(g => g.Key, g => g.First().InitiativeKey); // choose first if a ticket appears under multiple initiatives

        return this.ticketToInitiativeMap;
    }

    public IReadOnlyDictionary<string, string> LeafTicketToPmPlanMap()
    {
        if (!this.pmPlans.Any())
        {
            return new Dictionary<string, string>();
        }

        if (this.ticketToPmPlanMap.Any())
        {
            return this.ticketToPmPlanMap;
        }

        // We treat lowest-level leaf tickets as those that are not Epics (i.e., issue type != Epic). The pmPlans may already contain epics expanded to include their children.
        this.ticketToPmPlanMap = this.pmPlans
            .SelectMany(p => p.ChildTickets.OfType<BasicJiraTicket>() ?? [], (p, t) => new { PmPlanKey = p.Key, Ticket = t })
            .GroupBy(x => x.Ticket.Key)
            .ToDictionary(g => g.Key, g => g.First().PmPlanKey);

        return this.ticketToPmPlanMap;
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

        // Clear cached mappings since pmPlans and initiatives were updated
        this.ticketToPmPlanMap = new Dictionary<string, string>();
        this.ticketToInitiativeMap = new Dictionary<string, string>();

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
