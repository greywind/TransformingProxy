using System.Collections.Generic;

namespace TransformingProxy
{
    using RequestRules = Dictionary<string, Rule>;
    using ResponseRules = Dictionary<string, Rule>;
    
    public class EndpointRuleset
    {
        public RequestRules? Request { get; set; }
        public ResponseRules? Response { get; set; }
    }
}