using System.Text.Json;
using System.Text.Json.Nodes;

namespace BensEngineeringMetrics.Jira;

internal interface IJsonToJiraBasicTypeMapper
{
    AgileSprint CreateAgileSprintFromJsonNode(JsonNode? json);

    BasicJiraInitiative CreateBasicInitiativeFromJsonElement(JsonElement issue, string linkType);

    BasicJiraTicketWithParent CreateBasicTicketFromJsonElement(JsonElement issue);
}
