using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics;

public class FieldMappingWithParser<T> : FieldMapping<T>
{
    public required Func<dynamic, T?> Parser { get; init; }

    public override T? Parse(dynamic d)
    {
        return Parser(d);
    }
}
