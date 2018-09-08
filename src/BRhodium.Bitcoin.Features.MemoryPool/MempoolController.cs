using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using BRhodium.Node.Controllers;
using BRhodium.Node.Utilities;

namespace BRhodium.Bitcoin.Features.MemoryPool
{
    public class MempoolController : FeatureController
    {
        public MempoolManager MempoolManager { get; private set; }

        public MempoolController(MempoolManager mempoolManager)
        {
            Guard.NotNull(mempoolManager, nameof(mempoolManager));

            this.MempoolManager = mempoolManager;
        }
    }
}
