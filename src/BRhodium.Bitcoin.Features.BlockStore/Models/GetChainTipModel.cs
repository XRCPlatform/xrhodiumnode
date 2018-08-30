using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.BlockStore.Models
{
    public class GetChainTipModel
    {

        /// <summary>
        /// height of the chain tip
        /// </summary>
        /// <value>
        /// The height.
        /// </value>
        [JsonProperty(PropertyName = "height")]
        public int Height { get; set; }

        /// <summary>
        /// block hash of the tip.
        /// </summary>
        /// <value>
        /// The hash.
        /// </value>
        [JsonProperty(PropertyName = "hash")]
        public string Hash { get; set; }

        /// <summary>
        /// zero for main chain.
        /// </summary>
        /// <value>
        /// The length of the branch.
        /// </value>
        [JsonProperty(PropertyName = "branchlen")]
        public int BranchLen { get; set; }

        /// <summary>
        /// "active" for the main chain
        /// </summary>
        /// <value>
        /// The status.
        /// </value>
        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }
    }
}
