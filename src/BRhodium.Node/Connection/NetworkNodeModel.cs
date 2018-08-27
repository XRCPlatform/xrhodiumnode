using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Node.Connection
{
    public class NetworkNodeModel
    {
        /// <summary>
        /// The IP address of the node.
        /// </summary>
        [JsonProperty(PropertyName = "addednode")]
        public string AddedNode { get; set; }

        /// <summary>
        /// If connected
        /// </summary>
        [JsonProperty(PropertyName = "connected")]
        public bool Connected { get; set; }

        /// <summary>
        /// If connected
        /// </summary>
        [JsonProperty(PropertyName = "addresses")]
        public List<AddressConnection> Addresses { get; set; }
    }
}
