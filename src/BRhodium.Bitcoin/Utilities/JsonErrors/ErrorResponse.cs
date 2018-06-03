using System.Collections.Generic;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Utilities.JsonErrors
{
    public class ErrorResponse
    {
        [JsonProperty(PropertyName = "errors")]
        public List<ErrorModel> Errors { get; set; }
    }

    public class ErrorModel
    {
        [JsonProperty(PropertyName = "status")]
        public int Status { get; set; }

        [JsonProperty(PropertyName = "error")]
        public string ErrorCode { get; set; }

        [JsonProperty(PropertyName = "description")]
        public string Description { get; set; }
    }
}
