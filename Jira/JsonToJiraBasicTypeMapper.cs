using System.Text.Json;
using System.Text.Json.Nodes;

namespace BensEngineeringMetrics.Jira;

internal class JsonToJiraBasicTypeMapper : IJsonToJiraBasicTypeMapper
{
    public AgileSprint CreateAgileSprintFromJsonNode(JsonNode? json)
    {
        if (json is null)
        {
            throw new NotSupportedException("No Agile Sprint values returned from API.");
        }

        var records = json["total"]!.GetValue<int>();
        if (records == 0)
        {
            throw new NotSupportedException("No Agile Sprint values returned from API.");
        }

        var jsonValue = json["values"]?[0] ?? throw new NotSupportedException("No Agile Sprint values returned from API.");

        return new AgileSprint(
            jsonValue["id"]!.GetValue<int>(),
            jsonValue["state"]?.GetValue<string?>() ?? string.Empty,
            jsonValue["name"]?.GetValue<string?>() ?? string.Empty,
            jsonValue["startDate"]?.GetValue<DateTimeOffset?>() ?? DateTimeOffset.MaxValue,
            jsonValue["endDate"]?.GetValue<DateTimeOffset?>() ?? DateTimeOffset.MaxValue,
            CompleteDate: jsonValue["completeDate"]?.GetValue<DateTimeOffset?>() ?? DateTimeOffset.MaxValue,
            BoardId: jsonValue["originBoardId"]?.GetValue<int?>() ?? 0,
            Goal: jsonValue["goal"]?.GetValue<string?>() ?? string.Empty);
    }

    public BasicJiraInitiative CreateBasicInitiativeFromJsonElement(JsonElement issue, string linkType)
    {
        var key = issue.GetProperty(JiraFields.Key.Field).GetString();
        var summary = string.Empty;
        var status = string.Empty;
        bool? isReqdForGoLive = null;
        var issueKeyList = new List<IJiraKeyedIssue>();
        if (issue.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object)
        {
            summary = fields.GetProperty(JiraFields.Summary.Field).GetString();
            status = fields.GetProperty(JiraFields.Status.Field).GetProperty(JiraFields.Status.FlattenField).GetString();
            isReqdForGoLive = null;
            if (fields.TryGetProperty(JiraFields.IsReqdForGoLive.Field, out var cf) && cf.ValueKind != JsonValueKind.Null)
            {
                isReqdForGoLive = cf.GetDouble() > 0;
            }

            if (fields.TryGetProperty(JiraFields.InitiativeChildren.Field, out var issueLinks) && issueLinks.ValueKind == JsonValueKind.Array)
            {
                foreach (var link in issueLinks.EnumerateArray())
                {
                    if (!link.TryGetProperty(linkType, out var childIssue))
                    {
                        continue;
                    }

                    if (childIssue.TryGetProperty(JiraFields.Key.Field, out var outKey) && outKey.ValueKind == JsonValueKind.String)
                    {
                        if (!childIssue.TryGetProperty("fields", out var childIssueFields))
                        {
                            continue;
                        }

                        string linkIssueType;
                        if (childIssueFields.TryGetProperty(JiraFields.IssueType.Field, out var issueTypeObject))
                        {
                            linkIssueType = issueTypeObject.GetProperty(JiraFields.IssueType.FlattenField).GetString()!;
                        }
                        else
                        {
                            // Weirdly have observed the issuetype come through as 'type'
                            if (childIssueFields.TryGetProperty("type", out issueTypeObject))
                            {
                                linkIssueType = issueTypeObject.GetProperty("name").GetString()!;
                            }
                            else
                            {
                                linkIssueType = Constants.Unknown;
                            }
                        }

                        var childStatus = childIssueFields.GetProperty(JiraFields.Status.Field).GetProperty(JiraFields.Status.FlattenField).GetString();
                        var childSummary = childIssueFields.GetProperty(JiraFields.Summary.Field).GetString();

                        issueKeyList.Add(new BasicJiraTicket(outKey.GetString()!, childSummary ?? string.Empty, childStatus ?? Constants.Unknown, linkIssueType));
                        continue;
                    }

                    // Sometimes the link itself may contain a top-level "key" (defensive)
                    if (link.TryGetProperty("key", out var linkKey) && linkKey.ValueKind == JsonValueKind.String)
                    {
                        throw new NotSupportedException("I dont think this code is needed");
                        // var issueTypeObject = link.GetProperty("issuetype");
                        // issueKeyList.Add(new BasicJiraTicket(linkKey.GetString()!, linkIssueType));
                    }
                }
            }
        }

        var issueLinkKeys = issueKeyList.Distinct().ToArray();
        return new BasicJiraInitiative(
            key ?? string.Empty,
            summary ?? string.Empty,
            status ?? string.Empty,
            isReqdForGoLive ?? false,
            issueLinkKeys);
    }

    public BasicJiraTicketWithParent CreateBasicTicketFromJsonElement(JsonElement issue)
    {
        var key = issue.GetProperty(JiraFields.Key.Field).GetString();
        var summary = string.Empty;
        var status = string.Empty;
        var issueType = string.Empty;
        var parentKey = string.Empty;
        if (issue.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object)
        {
            summary = fields.GetProperty(JiraFields.Summary.Field).GetString();
            status = fields.GetProperty(JiraFields.Status.Field).GetProperty(JiraFields.Status.FlattenField).GetString();
            issueType = fields.GetProperty(JiraFields.IssueType.Field).GetProperty(JiraFields.IssueType.FlattenField).GetString();

            // Establish epic relationship to a parent
            if (fields.TryGetProperty(JiraFields.ParentKey.Field, out var parentLink) && parentLink.ValueKind == JsonValueKind.Object)
            {
                parentKey = parentLink.GetProperty(JiraFields.Key.Field).GetString();
            }
        }

        return new BasicJiraTicketWithParent(
            key ?? string.Empty,
            summary ?? string.Empty,
            status ?? Constants.Unknown,
            issueType ?? Constants.Unknown,
            parentKey ?? string.Empty);
    }
}
