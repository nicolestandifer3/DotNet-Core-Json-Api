using Newtonsoft.Json;

namespace JsonApiDotNetCore.Models.JsonApiDocuments
{
    public class ErrorSource
    {
        [JsonProperty("pointer")]
        public string Pointer { get; set; }

        [JsonProperty("parameter")]
        public string Parameter { get; set; }
    }
}
