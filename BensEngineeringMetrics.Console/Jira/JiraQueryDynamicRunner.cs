using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BensEngineeringMetrics.Jira;

internal class JiraQueryDynamicRunner(IJsonToJiraBasicTypeMapper jsonMapper, IApiClientFactory clientFactory) : IJiraQueryRunner
{
    /// <summary>
    ///     Used for mapping fields from Json into a dynamic object.
    /// </summary>
    private SortedList<string, IFieldMapping[]> fieldAliases = new();

    private string[] IgnoreFields => ["avatarId", "hierarchyLevel", "iconUrl", "id", "expand", "self", "subtask"];

    public async Task<AgileSprint?> GetCurrentSprintForBoard(int boardId)
    {
        var result = await clientFactory.CreateJiraApiClient().GetAgileBoardActiveSprintAsync(boardId);
        if (string.IsNullOrEmpty(result))
        {
            return null;
        }

        var json = JsonNode.Parse(result) ?? throw new NotSupportedException("No Agile Sprint values returned from API.");
        var pageValues = json["values"] ?? throw new NotSupportedException("No Agile Sprint values returned from API.");
        if (pageValues.AsArray().Count < 1)
        {
            throw new NotSupportedException("No Agile Sprint values returned from API.");
        }

        return jsonMapper.CreateAgileSprintFromJsonNode(pageValues[0]);
    }

    public async Task<IEnumerable<BasicJiraInitiative>> GetInitiatives(int monthsOfClosedInitiativesToFetch = 0)
    {
        var jql = """type = "Product Initiative" AND status NOT IN (Cancelled, "Feature Delivered") ORDER BY key""";
        IFieldMapping[] fields = [JiraFields.Summary, JiraFields.Status, JiraFields.IsReqdForGoLive, JiraFields.InitiativeChildren, JiraFields.PmPlanCustomer];
        var initiatives = new List<BasicJiraInitiative>();

        await GetSomethingFromJira(eachLinkedIssue =>
            {
                initiatives.Add(jsonMapper.CreateBasicInitiativeFromJsonElement(eachLinkedIssue, "outwardIssue", _ => true));
            },
            jql,
            fields);

        if (monthsOfClosedInitiativesToFetch > 0)
        {
            var weeksToFetch = monthsOfClosedInitiativesToFetch * 4;
            jql = $"""type = "Product Initiative" AND status IN (Cancelled, "Feature Delivered") AND status CHANGED TO (Cancelled, "Feature Delivered") AFTER -{weeksToFetch}w ORDER BY key""";
            await GetSomethingFromJira(eachLinkedIssue =>
                {
                    initiatives.Add(jsonMapper.CreateBasicInitiativeFromJsonElement(eachLinkedIssue, "outwardIssue", _ => true));
                },
                jql,
                fields);
        }

        return initiatives;
    }

    public async Task<IEnumerable<BasicJiraTicketWithParent>> GetEpicChildren(string[] epicKeys)
    {
        var jql = $"""parent IN ({string.Join(',', epicKeys)}) Order By key""";
        if (jql.Length < 25)
        {
            return [];
        }

        IFieldMapping[] fields = [JiraFields.IssueType, JiraFields.Summary, JiraFields.ParentKey, JiraFields.Status];
        var issues = new List<BasicJiraTicketWithParent>();

        await GetSomethingFromJira(eachIssue =>
            {
                issues.Add(jsonMapper.CreateBasicTicketFromJsonElement(eachIssue));
            },
            jql,
            fields);

        return issues;
    }

    public async Task<IEnumerable<BasicJiraPmPlan>> GetIdeas(int monthsOfClosedIdeasToFetch = 0)
    {
        var jql = """project = "PMPLAN" AND type = idea AND status NOT IN ("Feature delivered", Cancelled) ORDER BY key""";
        IFieldMapping[] fields = [JiraFields.Summary, JiraFields.Status, JiraFields.IsReqdForGoLive, JiraFields.InitiativeChildren];
        var initiatives = new List<BasicJiraPmPlan>();

        await GetSomethingFromJira(eachLinkedIssue =>
            {
                var temp = jsonMapper.CreateBasicInitiativeFromJsonElement(eachLinkedIssue, "inwardIssue", type => type != Constants.ProductInitiativeType);
                // Change type from BasicJiraInitiative to BasicJiraPmPlan - reduce duplicate code, they are very similar types, but useful to have type distinction.
                initiatives.Add(new BasicJiraPmPlan(temp.Key, temp.Summary, temp.Status, Constants.IdeaType, temp.RequiredForGoLive, temp.ChildPmPlans, temp.Customers));
            },
            jql,
            fields);

        if (monthsOfClosedIdeasToFetch > 0)
        {
            var weeksToFetch = monthsOfClosedIdeasToFetch * 4;
            jql = $"""project = "PMPLAN" AND type = idea AND status IN ("Feature delivered", Cancelled) AND status CHANGED TO (Cancelled, "Feature Delivered") AFTER -{weeksToFetch}w ORDER BY key""";
            await GetSomethingFromJira(eachLinkedIssue =>
                {
                    var temp = jsonMapper.CreateBasicInitiativeFromJsonElement(eachLinkedIssue, "inwardIssue", type => type != Constants.ProductInitiativeType);
                    // Change type from BasicJiraInitiative to BasicJiraPmPlan - reduce duplicate code, they are very similar types, but useful to have type distinction.
                    initiatives.Add(new BasicJiraPmPlan(temp.Key, temp.Summary, temp.Status, Constants.IdeaType, temp.RequiredForGoLive, temp.ChildPmPlans, temp.Customers));
                },
                jql,
                fields);
        }

        return initiatives;
    }

    public async Task<IReadOnlyList<dynamic>> SearchJiraIssuesWithJqlAsync(string jql, IFieldMapping[] fields)
    {
        string? nextPageToken = null;
        bool isLastPage;
        var client = clientFactory.CreateJiraApiClient();
        var results = new List<dynamic>();

        this.fieldAliases = new SortedList<string, IFieldMapping[]>();
        foreach (var field in fields)
        {
            if (this.fieldAliases.ContainsKey(field.Field))
            {
                // This is to cater for flattening a single JsonObject into two or more fields. Ex: Sprint - need sprint name and sprint start date.
                this.fieldAliases[field.Field] = this.fieldAliases[field.Field].Append(field).ToArray();
            }
            else
            {
                this.fieldAliases.Add(field.Field, [field]);
            }
        }

        do
        {
            var responseJson = await client.PostSearchJqlAsync(jql, fields.Select(x => x.Field).ToArray(), nextPageToken);

            using var doc = JsonDocument.Parse(responseJson);
            var issues = doc.RootElement.GetProperty("issues");
            isLastPage = doc.RootElement.TryGetProperty("isLast", out var isLastPageToken) && isLastPageToken.GetBoolean();
            nextPageToken = doc.RootElement.TryGetProperty("nextPageToken", out var token) ? token.GetString() : null;

            foreach (var issue in issues.EnumerateArray())
            {
                results.Add(DeserializeToDynamic(issue, string.Empty));
            }
        } while (!isLastPage || nextPageToken != null);

        return results;
    }

    public async Task<AgileSprint?> GetSprintById(int sprintId)
    {
        var result = await clientFactory.CreateJiraApiClient().GetAgileBoardSprintByIdAsync(sprintId);
        if (string.IsNullOrEmpty(result))
        {
            return null;
        }

        return jsonMapper.CreateAgileSprintFromJsonNode(JsonNode.Parse(result));
    }

    public async Task<IReadOnlyList<AgileSprint>> GetAllSprints(int boardId)
    {
        var values = new List<JsonNode>();
        var apiClient = clientFactory.CreateJiraApiClient();

        var start = 0;
        var pageSize = 50;

        while (true)
        {
            var responseJson = await apiClient.GetAgileBoardAllSprintsAsync(boardId, start, pageSize);

            if (string.IsNullOrEmpty(responseJson))
            {
                break;
            }

            var json = JsonNode.Parse(responseJson);
            if (json is null)
            {
                break;
            }

            var pageValues = json["values"];
            if (pageValues is null)
            {
                break;
            }

            var arr = pageValues.AsArray();
            foreach (var jsonValue in arr)
            {
                if (jsonValue is null)
                {
                    continue;
                }

                values.Add(jsonValue);
            }

            var isLastPage = json["isLast"]?.GetValue<bool>() ?? false;
            if (isLastPage)
            {
                break;
            }

            if (arr.Count < pageSize)
            {
                break;
            }

            start += pageSize;
        }

        var sprints = new List<AgileSprint>();
        foreach (var jsonValue in values)
        {
            sprints.Add(jsonMapper.CreateAgileSprintFromJsonNode(jsonValue));
        }

        return sprints.OrderByDescending(s => s.StartDate).ToList();
    }

    private dynamic DeserialiseDynamicArray(JsonElement element, string propertyName, string childField)
    {
        var list = new List<object>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(DeserializeToDynamic(item, propertyName));
        }

        if (string.IsNullOrEmpty(childField))
        {
            return string.Join(",", list);
        }

        var flattened = list
            .OfType<IDictionary<string, object>>()
            .Select(obj => obj.TryGetValue(childField, out var value) ? value : null)
            .Where(x => x != null);
        return string.Join(",", flattened);
    }

    private IDictionary<string, object> DeserialiseDynamicObject(JsonElement element)
    {
        var expando = new ExpandoObject() as IDictionary<string, object>;
        foreach (var prop in element.EnumerateObject())
        {
            // Special handling for 'fields' property - it's useful to flatten it
            if (prop is { Name: "fields", Value.ValueKind: JsonValueKind.Object })
            {
                // Flatten 'fields' properties into the parent object
                foreach (var fieldProp in prop.Value.EnumerateObject())
                {
                    if (fieldProp.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (PropertyShouldBeFlattened(fieldProp.Name, out var childFields1))
                        {
                            foreach (var childField in childFields1)
                            {
                                // Extract the childField property from the issueType object
                                if (fieldProp.Value.TryGetProperty(childField, out var childFieldValue) && childFieldValue.ValueKind == JsonValueKind.String)
                                {
                                    expando[FieldName(fieldProp.Name, childField)] = childFieldValue.GetString()!;
                                }
                            }

                            continue;
                        }
                    }

                    if (PropertyShouldBeFlattened(fieldProp.Name, out var childFields2))
                    {
                        foreach (var childField in childFields2)
                        {
                            expando[FieldName(fieldProp.Name, childField)] = DeserializeToDynamic(fieldProp.Value, fieldProp.Name, childField);
                        }
                    }
                    else
                    {
                        expando[FieldName(fieldProp.Name)] = DeserializeToDynamic(fieldProp.Value, fieldProp.Name);
                    }
                }

                continue;
            }

            if (IgnoreFields.Contains(prop.Name))
            {
                continue; // Skip fields that are in the ignore list
            }

            expando[FieldName(prop.Name)] = DeserializeToDynamic(prop.Value, prop.Name);
        }

        return expando;
    }

    private dynamic DeserializeToDynamic(JsonElement element, string propertyName, string childField = "")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return DeserialiseDynamicObject(element);

            case JsonValueKind.Array:
                return DeserialiseDynamicArray(element, propertyName, childField);

            case JsonValueKind.String:
                var elementString = element.GetString();
                if (DateTimeOffset.TryParse(elementString, out var dto))
                {
                    return dto.ToLocalTime(); // Dates coming thru as UTC, convert to local time (is reversible and keeps timezone info)
                }

                return elementString!;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))
                {
                    return l;
                }

                if (element.TryGetDouble(out var d))
                {
                    return d;
                }

                return element.GetDecimal();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null!;

            default:
                return element.GetRawText();
        }
    }

    private string FieldName(string field, string childField = "")
    {
        if (this.fieldAliases.TryGetValue(field, out var mappings))
        {
            var mapping = mappings.FirstOrDefault(m => m.FlattenField == childField);
            if (mapping is null)
            {
                mapping = mappings.First();
            }

            return string.IsNullOrEmpty(mapping.Alias) ? field : mapping.Alias;
        }

        return field;
    }

    private async Task GetSomethingFromJira(Action<JsonElement> callBackActionForEachIssue, string jql, IFieldMapping[] fields)
    {
        string? nextPageToken = null;
        bool isLastPage;
        var client = clientFactory.CreateJiraApiClient();
        do
        {
            var responseJson = await client.PostSearchJqlAsync(jql, fields.Select(f => f.Field).ToArray(), nextPageToken);

            using var doc = JsonDocument.Parse(responseJson);
            var issues = doc.RootElement.GetProperty("issues");
            isLastPage = doc.RootElement.TryGetProperty("isLast", out var isLastPageToken) && isLastPageToken.GetBoolean();
            nextPageToken = doc.RootElement.TryGetProperty("nextPageToken", out var token) ? token.GetString() : null;

            foreach (var issue in issues.EnumerateArray())
            {
                callBackActionForEachIssue(issue);
            }
        } while (!isLastPage || nextPageToken != null);
    }

    private bool PropertyShouldBeFlattened(string field, out IEnumerable<string> childFields)
    {
        if (this.fieldAliases.TryGetValue(field, out var mapping))
        {
            childFields = mapping.Select(m => m.FlattenField).Where(f => !string.IsNullOrEmpty(f));
            return childFields.Any();
        }

        childFields = [];
        return false;
    }
}
