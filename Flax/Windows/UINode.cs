using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Flax.Windows
{
    /// <summary>
    /// Serializable snapshot of a UI element used to emit a token-efficient
    /// JSON tree for LLM consumption. Empty optional fields and empty child
    /// lists are omitted from the output.
    /// </summary>
    public class UINode
    {
        public int Id { get; set; }
        public string ControlType { get; set; }
        public string Name { get; set; }
        public string AutomationId { get; set; }
        public string ClassName { get; set; }
        public int[] Rect { get; set; }
        public bool Enabled { get; set; }
        public bool Visible { get; set; }
        public List<UINode> Children { get; set; }

        public string ToJson()
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            return JsonConvert.SerializeObject(this, settings);
        }
    }
}
