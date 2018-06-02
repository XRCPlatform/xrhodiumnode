using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BRhodium.Bitcoin.Features.Consensus.Models
{

    public class TransactionModel
    {
        public double Amount { get; set; } // seems it's set to 0 for immature transactions or generation transactions.

        /// <summary>
        /// As Amount may not always return the total transaction amount, TotalAmount calculates and return the value using transaction details
        /// </summary>
        public double TotalAmount { get { return Details.Sum(item => item.Amount); } }

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
    }   

    
}
