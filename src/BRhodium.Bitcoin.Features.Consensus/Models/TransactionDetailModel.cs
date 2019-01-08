using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Consensus.Models
{
    public class TransactionDetail
    {
        public string Account { get; set; }
        public string Address { get; set; }
        public string Category { get; set; }
        public decimal Amount { get; set; }
        [DefaultValue(0D)]
        [JsonProperty(PropertyName = "Fee", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal Fee { get; set; }
    }
}
