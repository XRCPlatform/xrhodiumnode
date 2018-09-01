using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.MemoryPool.Models
{
    public class GetMemPoolEntry
    {
        [JsonProperty(PropertyName = "size")]
        public long Size { get; set; }

        [JsonProperty(PropertyName = "fee")]
        public decimal Fee { get; set; }

        [JsonProperty(PropertyName = "modifiedfee")]
        public long ModifiedFee { get; set; }

        [JsonProperty(PropertyName = "time")]
        public long Time { get; set; }

        [JsonProperty(PropertyName = "height")]
        public int Height { get; set; }

        [JsonProperty(PropertyName = "descendantcount")]
        public long DescendantCount { get; set; }

        [JsonProperty(PropertyName = "descendantsize")]
        public long DescendantSize { get; set; }

        [JsonProperty(PropertyName = "descendantfees")]
        public decimal DescendantFees { get; set; }

        [JsonProperty(PropertyName = "ancestorcount")]
        public long AncestorCount { get; set; }

        [JsonProperty(PropertyName = "ancestorsize")]
        public long AncestorSize { get; set; }

        [JsonProperty(PropertyName = "ancestorfees")]
        public decimal AncestorFees { get; set; }

        [JsonProperty(PropertyName = "wtxid")]
        public string WtxId { get; set; }

        [JsonProperty(PropertyName = "depends")]
        public List<string> Depends { get; set; }
    }
}
