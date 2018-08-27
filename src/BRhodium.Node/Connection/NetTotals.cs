using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Node.Connection
{
    public class NetTotals
    {
        /// <summary>
        /// Gets or sets the total bytes recv.
        /// </summary>
        /// <value>
        /// The total bytes recv.
        /// </value>
        [JsonProperty(PropertyName = "totalbytesrecv")]
        public long TotalBytesRecv { get; set; }

        /// <summary>
        /// Gets or sets the total bytes sent.
        /// </summary>
        /// <value>
        /// The total bytes sent.
        /// </value>
        [JsonProperty(PropertyName = "totalbytessent")]
        public long TotalBytesSent { get; set; }

        /// <summary>
        /// Gets or sets the timesec.
        /// </summary>
        /// <value>
        /// The timesec.
        /// </value>
        [JsonProperty(PropertyName = "timesec")]
        public long Timesec { get; set; }

        /// <summary>
        /// Gets or sets the timemillis.
        /// </summary>
        /// <value>
        /// The timesec.
        /// </value>
        [JsonProperty(PropertyName = "timemillis")]
        public long Timemillis { get; set; }
    }
}