using System.Diagnostics;
using BensEngineeringMetrics.Jira;
using BensEngineeringMetrics.Tasks;
using BensEngineeringMetrics.Test.TestHarnesses;
using NSubstitute;

namespace BensEngineeringMetrics.Test;

public class CalculatePmPlanReleaseBurnUpValuesTest
{
    [Fact(Skip = "Only run manually")]
    //[Fact]
    public async Task Recorder()
    {
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
        var mapper = new JsonToJiraBasicTypeMapper();
        var harnessFactory = new JiraApiClientFactoryTestHarness("CalculatePmPlanReleasesBurnUpValuesLogs");
        var runner = new JiraQueryDynamicRunner(mapper, harnessFactory);
        var envestPmPlanStories = new EnvestPmPlanStories(runner);
        var mockExporter = Substitute.For<ICsvExporter>();

        var sut = new CalculatePmPlanReleaseBurnUpValues(mockExporter, envestPmPlanStories);

        await sut.ExecuteAsync(["TASKID"]);
        Debugger.Break();
        Assert.Fail();
    }
}
