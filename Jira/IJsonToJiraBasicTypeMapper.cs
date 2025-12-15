using System.Text.Json;
using System.Text.Json.Nodes;

namespace BensEngineeringMetrics.Jira;

/// <summary>
///     A mapper class to encapsulate all JSON to Jira type mapper functions. These are primarily used when its not ideal to use the <see cref="JiraQueryDynamicRunner" />
/// </summary>
internal interface IJsonToJiraBasicTypeMapper
{
    AgileSprint CreateAgileSprintFromJsonNode(JsonNode? json);

    BasicJiraInitiative CreateBasicInitiativeFromJsonElement(JsonElement issue, string linkType, Predicate<string> excludeParentFilter);

    BasicJiraTicketWithParent CreateBasicTicketFromJsonElement(JsonElement issue);
}
