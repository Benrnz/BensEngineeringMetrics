using System.Diagnostics;
using BensEngineeringMetrics.Jira;
using BensEngineeringMetrics.Tasks;
using BensEngineeringMetrics.Test.TestHarnesses;
using NSubstitute;

namespace BensEngineeringMetrics.Test;

public class CalculatePmPlanReleaseBurnUpValuesTest
{
    //[Fact(Skip = "Only run manually")]
    [Fact]
    public async Task Recorder()
    {
        // This test is intended to put the JiraApiClient into recording mode to capture live data for later playback during a test.
        // This approach allows for refactoring of downstream code from the API and ensure results are the same.

        var mapper = new JsonToJiraBasicTypeMapper();
        var factory = new JiraApiClientFactory(true);
        var runner = new JiraQueryDynamicRunner(mapper, factory);
        var envestPmPlanStories = new EnvestPmPlanStories(runner);
        // No need to export to csv, stub this out.
        var mockExporter = Substitute.For<ICsvExporter>();

        var sut = new CalculatePmPlanReleaseBurnUpValues(mockExporter, envestPmPlanStories);

        await sut.ExecuteAsync(["TASKID"]);
    }

    [Fact]
    public async Task Test1()
    {
        // This test is reliant on the above recorder to have been run and stored json files locally inside this test project.
        var mapper = new JsonToJiraBasicTypeMapper();
        var harnessFactory = new JiraApiClientFactoryTestHarness("CalculatePmPlanReleasesBurnUpValuesLogs"); // The folder where the json log recorded files are stored.
        var runner = new JiraQueryDynamicRunner(mapper, harnessFactory);
        var envestPmPlanStories = new EnvestPmPlanStories(runner);
        var mockExporter = Substitute.For<ICsvExporter>();

        var sut = new CalculatePmPlanReleaseBurnUpValues(mockExporter, envestPmPlanStories);

        await sut.ExecuteAsync(["TASKID"]);

        /*
         Data to validate this test against.  This was captured from the live run hitting the API and log files were created during that run.
            As at 20/01/2026
            Total work to be done: 1733.05
            Work completed: 1486.05
            PmPlans with High level estimates only: 28
            PmPlans with no estimate: 11
            PmPlans with Spec'ed and Estimated: 24
            Stories with no estimate: 167 / 292
            Average Velocity (last 6 weeks): 29.7 story points per sprint
         */
        Assert.Fail();
    }
}
