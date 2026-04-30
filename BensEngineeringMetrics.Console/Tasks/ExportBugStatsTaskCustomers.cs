namespace BensEngineeringMetrics.Tasks;

public class ExportBugStatsTaskCustomers(BugStatsCustomerWorker bugStatsCustomerWorker, IOutputter outputter) : IEngineeringMetricsTask
{
    // JAVPM Bug Analysis
    private const string NzbGoogleSheetId = "1kAdXDPqn-avk21IZ7fLqKuqbs69UazKcns6M5VIol4I";
    private const string EnvestGoogleSheetId = "1Na2kv5ADLwKZ6IVFV_nUY8BQhYccMiC3ioaYhLRmdwk";
    private const string KeyString = "BUG_STATS_CUSTOMERS";

    private static readonly BugStatsCustomerProfile NzbProfile = new(
        """AND "Customer/s (Multi Select)[Select List (multiple choices)]" = NZbrokers """,
        "NZb");

    private static readonly BugStatsCustomerProfile EnvestProfile = new(
        """AND ("Customer/s (Multi Select)[Select List (multiple choices)]" = Envest OR "Exalate[Short text]" ~ Envest) """,
        "Envest");

    public string Key => KeyString;
    public string Description => "Export a series of exports summarising _bug_stats_ for JAVPM Major clients Only.";

    public async Task ExecuteAsync(string[] args)
    {
        outputter.WriteLine($"{Key} - {Description}");
        outputter.WriteLine($"--------------------- {Constants.JavPmJiraProjectKey} ---------------------");

        outputter.WriteLine();
        outputter.WriteLine("NZ Brokers:");
        await bugStatsCustomerWorker.UpdateSheet(Constants.JavPmJiraProjectKey, NzbGoogleSheetId, NzbProfile);

        outputter.WriteLine();
        outputter.WriteLine("Envest:");
        await bugStatsCustomerWorker.UpdateSheet(Constants.JavPmJiraProjectKey, EnvestGoogleSheetId, EnvestProfile);
    }
}
