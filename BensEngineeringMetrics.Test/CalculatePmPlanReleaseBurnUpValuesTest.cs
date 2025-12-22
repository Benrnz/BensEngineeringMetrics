using BensEngineeringMetrics.Jira;
using BensEngineeringMetrics.Test.TestHarnesses;
using BensEngineeringMetrics.Tasks;
using NSubstitute;

namespace BensEngineeringMetrics.Test;

public class CalculatePmPlanReleaseBurnUpValuesTest
{
    [Fact]
    public async Task Test1()
    {
        var mapper = new JsonToJiraBasicTypeMapper();
        var harnessFactory = new JiraApiClientFactoryTestHarness();
        var runner = new JiraQueryDynamicRunner(mapper, harnessFactory);
        var envestPmPlanStories = new EnvestPmPlanStories(runner);
        var mockExporter = Substitute.For<ICsvExporter>();

        var sut = new CalculatePmPlanReleaseBurnUpValues(mockExporter, envestPmPlanStories);

        await sut.ExecuteAsync(["TASKID"]);
    }
}
