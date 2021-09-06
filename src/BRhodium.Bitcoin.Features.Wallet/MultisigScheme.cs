using System;
using System.Collections.Generic;
using System.Text;

namespace BRhodium.Bitcoin.Features.Wallet
{
    [Serializable]
    public class MultisigScheme
    {
        /// <summary>
        /// How many signatures will be suffient to move the funds.
        /// </summary>
        public int Threashold { get; set; }

        /// <summary>
        /// Cosigner extended pubkeys. Intentinaly not including any xPriv at this stage as such model is simplest to start with.
        /// </summary>
        public string[] XPubs { get; set; }
    }
}
