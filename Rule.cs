using System.Text.Json.Serialization;

namespace TransformingProxy
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RuleAction
    {
        Clear,
        Set,
    }

    public class Rule
    {
        public RuleAction Action { get;  init; }
        public object? Value { get; init; }
    }
}