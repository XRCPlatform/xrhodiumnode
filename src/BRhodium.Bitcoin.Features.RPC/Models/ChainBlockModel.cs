using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.RPC.Models
{
    public class ChainBlockModel
    {
        [JsonProperty(Order = 0, PropertyName = "height")]
        public int Height { get; set; }

        [JsonProperty(Order = 1, PropertyName = "age")]
        public DateTimeOffset Age { get; set; }

        [JsonProperty(Order = 2, PropertyName = "transactions")]
        public List<TransactionChainBlockModel> Transactions { get; set; }

        [JsonProperty(Order = 3, PropertyName = "totalsatoshi")]
        public double TotalSatoshi { get; set; }

        [JsonProperty(Order = 4, PropertyName = "size")]
        public int Size { get; set; }

        [JsonProperty(Order = 5, PropertyName = "hash")]
        public string Hash { get; set; }

        [JsonProperty(Order = 6, PropertyName = "bits")]
        public string Bits { get; set; }

        [JsonProperty(Order = 7, PropertyName = "version")]
        public int Version { get; set; }

        [JsonProperty(Order = 8, PropertyName = "fees")]
        public double TransactionFees { get; set; }

        [JsonProperty(Order = 9, PropertyName = "difficult")]
        public double Difficult { get; set; }

        [JsonProperty(Order = 10, PropertyName = "prevhash")]
        public string PrevHash { get; set; }

        [JsonProperty(Order = 11, PropertyName = "nexthash")]
        public string NextHash { get; set; }

        [JsonProperty(Order = 12, PropertyName = "merkleroot")]
        public string MerkleRoot { get; set; }
    }

    public class TransactionChainBlockModel
    {
        [JsonProperty(Order = 0, PropertyName = "hash")]
        public string Hash { get; set; }

        [JsonProperty(Order = 1, PropertyName = "satoshi")]
        public double Satoshi { get; set; }

        [JsonProperty(Order = 2, PropertyName = "addressfrom")]
        public List<TransactionAddressModel> AddressFrom { get; set; }

        [JsonProperty(Order = 2, PropertyName = "addressto")]
        public List<TransactionAddressModel> AddressTo { get; set; }
    }

    public class TransactionAddressModel
    {
        [JsonProperty(Order = 0, PropertyName = "address")]
        public string Address { get; set; }

        [JsonProperty(Order = 1, PropertyName = "satoshi")]
        public double Satoshi { get; set; }
    }
}
