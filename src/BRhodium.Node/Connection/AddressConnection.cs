using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Node.Connection
{
    public class AddressConnection
    {
        /// <summary>
        /// Address:Port of connection
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// Connection, inbound or outbound
        /// </summary>
        [JsonProperty(PropertyName = "connected")]
        public string Connected { get; set; }
    }
}
