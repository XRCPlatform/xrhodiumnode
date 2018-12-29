using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Consensus.Models
{

    public class TransactionModel
    {
        public double TotalAmount { get; set; }

        public int Confirmations { get; set; }

        public bool Generated { get; set; }

        public string BlockHash { get; set; }

        public int BlockIndex { get; set; }

        public long BlockTime { get; set; }

        public string TxId { get; set; }

        public string NormTxId { get; set; }

        public long Time { get; set; }
        public long TimeReceived { get; set; }

        public List<TransactionDetail> Details { get; set; }
        public string Hex { get; set; }

        [DefaultValue(0)]
        [JsonProperty(PropertyName = "Fee", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal Fee { get; set; }
    }   

    
}
