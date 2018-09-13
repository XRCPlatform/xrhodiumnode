using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.RPC.Models
{
    public class GetNetworkInfoModel
    {
        [JsonProperty(PropertyName = "version")]
        public uint Version { get; set; }

        [JsonProperty(PropertyName = "subversion")]
        public string SubVersion { get; set; }

        [JsonProperty( PropertyName = "protocolversion")]
        public uint ProtocolVersion { get; set; }

        [JsonProperty(PropertyName = "localservices")]
        public string LocalServices { get; set; }

        [JsonProperty(PropertyName = "localrelay")]
        public bool LocalRelay { get; set; }

        [JsonProperty(PropertyName = "timeoffset")]
        public long TimeOffset { get; set; }

        [JsonProperty( PropertyName = "connections", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Connections { get; set; }

        [JsonProperty(PropertyName = "networkactive", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? NetworkActive { get; set; }

        [JsonProperty(PropertyName = "networks")]
        public List<GetNetworkInfoNetworkModel> Networks { get; set; }

        [JsonProperty(PropertyName = "relayfee")]
        public decimal RelayFee { get; set; }

        [JsonProperty(PropertyName = "incrementalfee")]
        public decimal IncrementalFee { get; set; }

        [JsonProperty(PropertyName = "localaddresses")]
        public List<GetNetworkInfoAddressModel> LocalAddresses { get; set; }

        [JsonProperty(PropertyName = "warning")]
        public string Warning { get; set; }

    }

    public class GetNetworkInfoAddressModel
    {
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        [JsonProperty(PropertyName = "port")]
        public int Port { get; set; }
    }

    public class GetNetworkInfoNetworkModel
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "limited")]
        public bool Limited { get; set; }

        [JsonProperty(PropertyName = "reachable")]
        public bool Reachable { get; set; }

        [JsonProperty(PropertyName = "proxy")]
        public string Proxy { get; set; }

        [JsonProperty(PropertyName = "proxy_randomize_credentials")]
        public bool ProxyRandomizeCredentials { get; set; }
    }
}
