using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.MemoryPool.Fee
{
    /// <summary>
    /// Transation confirmation statistics.
    /// </summary>
    public class TxConfirmStats
    {
        /// <summary>Used only for dbreeze storage</summary>
        public uint Id { get; set; }

        /// <summary>Instance logger for logging messages.</summary>
        private ILogger logger;

        /// <summary>
        /// Moving average of total fee rate of all transactions in each bucket.
        /// </summary>
        /// <remarks>
        /// Track the historical moving average of this total over blocks.
        /// </remarks>
        public List<double> Avg { get; set; }

        /// <summary>Map of bucket upper-bound to index into all vectors by bucket.</summary>
        public Dictionary<double, int> BucketMap { get; set; }

        //Define the buckets we will group transactions into.

        /// <summary>The upper-bound of the range for the bucket (inclusive).</summary>
        public List<double> Buckets { get; set; }

        // Count the total # of txs confirmed within Y blocks in each bucket.
        // Track the historical moving average of theses totals over blocks.

        /// <summary>Confirmation average. confAvg[Y][X].</summary>
        public List<List<double>> ConfAvg { get; set; }

        /// <summary>Current block confirmations. curBlockConf[Y][X].</summary>
        public List<List<int>> CurBlockConf { get; set; }

        /// <summary>Current block transaction count.</summary>
        public List<int> CurBlockTxCt { get; set; }

        /// <summary>Current block fee rate.</summary>
        public List<double> CurBlockVal { get; set; }

        // Combine the conf counts with tx counts to calculate the confirmation % for each Y,X
        // Combine the total value with the tx counts to calculate the avg feerate per bucket

        /// <summary>Decay value to use.</summary>
        public double Decay { get; set; }

        /// <summary>Transactions still unconfirmed after MAX_CONFIRMS for each bucket</summary>
        public List<int> OldUnconfTxs { get; set; }

        /// <summary>
        /// Historical moving average of transaction counts.
        /// </summary>
        /// <remarks>
        /// For each bucket X:
        /// Count the total # of txs in each bucket.
        /// Track the historical moving average of this total over blocks
        /// </remarks>
        public List<double> TxCtAvg { get; set; }

        /// <summary>
        /// Mempool counts of outstanding transactions.
        /// </summary>
        /// <remarks>
        /// For each bucket X, track the number of transactions in the mempool
        /// that are unconfirmed for each possible confirmation value Y
        /// unconfTxs[Y][X]
        /// </remarks>
        public List<List<int>> UnconfTxs { get; set; }

        public TxConfirmStats()
        {

        }

        /// <summary>
        /// Constructs an instance of the transaction confirmation stats object.
        /// </summary>
        /// <param name="logger">Instance logger to use for message logging.</param>
        public TxConfirmStats(ILogger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Initialize the data structures.  This is called by BlockPolicyEstimator's
        /// constructor with default values.
        /// </summary>
        /// <param name="defaultBuckets">Contains the upper limits for the bucket boundaries.</param>
        /// <param name="maxConfirms">Max number of confirms to track.</param>
        /// <param name="decay">How much to decay the historical moving average per block.</param>
        public void Initialize(List<double> defaultBuckets, int maxConfirms, double decay)
        {
            this.Buckets = new List<double>();
            this.BucketMap = new Dictionary<double, int>();

            this.Decay = decay;
            for (int i = 0; i < defaultBuckets.Count; i++)
            {
                this.Buckets.Add(defaultBuckets[i]);
                this.BucketMap[defaultBuckets[i]] = i;
            }
            this.ConfAvg = new List<List<double>>();
            this.CurBlockConf = new List<List<int>>();
            this.UnconfTxs = new List<List<int>>();

            for (int i = 0; i < maxConfirms; i++)
            {
                this.ConfAvg.Insert(i, Enumerable.Repeat(default(double), this.Buckets.Count).ToList());
                this.CurBlockConf.Insert(i, Enumerable.Repeat(default(int), this.Buckets.Count).ToList());
                this.UnconfTxs.Insert(i, Enumerable.Repeat(default(int), this.Buckets.Count).ToList());
            }

            this.OldUnconfTxs = new List<int>(Enumerable.Repeat(default(int), this.Buckets.Count));
            this.CurBlockTxCt = new List<int>(Enumerable.Repeat(default(int), this.Buckets.Count));
            this.TxCtAvg = new List<double>(Enumerable.Repeat(default(double), this.Buckets.Count));
            this.CurBlockVal = new List<double>(Enumerable.Repeat(default(double), this.Buckets.Count));
            this.Avg = new List<double>(Enumerable.Repeat(default(double), this.Buckets.Count));
        }

        /// <summary>
        /// Clear the state of the curBlock variables to start counting for the new block.
        /// </summary>
        /// <param name="nBlockHeight">Block height.</param>
        public void ClearCurrent(int nBlockHeight)
        {
            for (var j = 0; j < this.Buckets.Count; j++)
            {
                this.OldUnconfTxs[j] += this.UnconfTxs[nBlockHeight % this.UnconfTxs.Count][j];
                this.UnconfTxs[nBlockHeight % this.UnconfTxs.Count][j] = 0;
                for (int i = 0; i < this.CurBlockConf.Count; i++)
                    this.CurBlockConf[i][j] = 0;
                this.CurBlockTxCt[j] = 0;
                this.CurBlockVal[j] = 0;
            }
        }

        /// <summary>
        /// Record a new transaction data point in the current block stats.
        /// </summary>
        /// <param name="blocksToConfirm">The number of blocks it took this transaction to confirm. blocksToConfirm is 1-based and has to be >= 1.</param>
        /// <param name="val">The feerate of the transaction.</param>
        public void Record(int blocksToConfirm, double val)
        {
            // blocksToConfirm is 1-based
            if (blocksToConfirm < 1)
                return;
            int bucketindex = this.BucketMap.FirstOrDefault(k => k.Key > val).Value;
            for (int i = blocksToConfirm; i <= this.CurBlockConf.Count; i++)
                this.CurBlockConf[i - 1][bucketindex]++;
            this.CurBlockTxCt[bucketindex]++;
            this.CurBlockVal[bucketindex] += val;
        }

        /// <summary>
        /// Record a new transaction entering the mempool.
        /// </summary>
        /// <param name="nBlockHeight">The block height.</param>
        /// <param name="val"></param>
        /// <returns>The feerate of the transaction.</returns>
        public int NewTx(int nBlockHeight, double val)
        {
            int bucketindex = this.BucketMap.FirstOrDefault(k => k.Key > val).Value;
            int blockIndex = nBlockHeight % this.UnconfTxs.Count;
            this.UnconfTxs[blockIndex][bucketindex]++;
            return bucketindex;
        }

        /// <summary>
        /// Remove a transaction from mempool tracking stats.
        /// </summary>
        /// <param name="entryHeight">The height of the mempool entry.</param>
        /// <param name="nBestSeenHeight">The best sceen height.</param>
        /// <param name="bucketIndex">The bucket index.</param>
        public void RemoveTx(int entryHeight, int nBestSeenHeight, int bucketIndex)
        {
            //nBestSeenHeight is not updated yet for the new block
            int blocksAgo = nBestSeenHeight - entryHeight;
            if (nBestSeenHeight == 0) // the BlockPolicyEstimator hasn't seen any blocks yet
                blocksAgo = 0;
            if (blocksAgo < 0)
            {
                this.logger.LogInformation($"Blockpolicy error, blocks ago is negative for mempool tx");
                return; //This can't happen because we call this with our best seen height, no entries can have higher
            }

            if (blocksAgo >= this.UnconfTxs.Count)
            {
                if (this.OldUnconfTxs[bucketIndex] > 0)
                    this.OldUnconfTxs[bucketIndex]--;
                else
                    this.logger.LogInformation(
                        $"Blockpolicy error, mempool tx removed from >25 blocks,bucketIndex={bucketIndex} already");
            }
            else
            {
                int blockIndex = entryHeight % this.UnconfTxs.Count;
                if (this.UnconfTxs[blockIndex][bucketIndex] > 0)
                    this.UnconfTxs[blockIndex][bucketIndex]--;
                else
                    this.logger.LogInformation(
                        $"Blockpolicy error, mempool tx removed from blockIndex={blockIndex},bucketIndex={bucketIndex} already");
            }
        }

        /// <summary>
        /// Update our estimates by decaying our historical moving average and updating
        /// with the data gathered from the current block.
        /// </summary>
        public void UpdateMovingAverages()
        {
            for (var j = 0; j < this.Buckets.Count; j++)
            {
                for (var i = 0; i < this.ConfAvg.Count; i++)
                    this.ConfAvg[i][j] = this.ConfAvg[i][j] * this.Decay + this.CurBlockConf[i][j];
                this.Avg[j] = this.Avg[j] * this.Decay + this.CurBlockVal[j];
                this.TxCtAvg[j] = this.TxCtAvg[j] * this.Decay + this.CurBlockTxCt[j];
            }
        }

        /// <summary>
        /// Calculate a feerate estimate.  Find the lowest value bucket (or range of buckets
        /// to make sure we have enough data points) whose transactions still have sufficient likelihood
        /// of being confirmed within the target number of confirmations.
        /// </summary>
        /// <param name="confTarget">Target number of confirmations.</param>
        /// <param name="sufficientTxVal">Required average number of transactions per block in a bucket range.</param>
        /// <param name="successBreakPoint">The success probability we require.</param>
        /// <param name="requireGreater">Return the lowest feerate such that all higher values pass minSuccess OR return the highest feerate such that all lower values fail minSuccess.</param>
        /// <param name="nBlockHeight">The current block height.</param>
        /// <returns></returns>
        public double EstimateMedianVal(int confTarget, double sufficientTxVal, double successBreakPoint,
            bool requireGreater,
            int nBlockHeight)
        {
            // Counters for a bucket (or range of buckets)
            double nConf = 0; // Number of tx's confirmed within the confTarget
            double totalNum = 0; // Total number of tx's that were ever confirmed
            int extraNum = 0; // Number of tx's still in mempool for confTarget or longer

            int maxbucketindex = this.Buckets.Count - 1;

            // requireGreater means we are looking for the lowest feerate such that all higher
            // values pass, so we start at maxbucketindex (highest feerate) and look at successively
            // smaller buckets until we reach failure.  Otherwise, we are looking for the highest
            // feerate such that all lower values fail, and we go in the opposite direction.
            int startbucket = requireGreater ? maxbucketindex : 0;
            int step = requireGreater ? -1 : 1;

            // We'll combine buckets until we have enough samples.
            // The near and far variables will define the range we've combined
            // The best variables are the last range we saw which still had a high
            // enough confirmation rate to count as success.
            // The cur variables are the current range we're counting.
            int curNearBucket = startbucket;
            int bestNearBucket = startbucket;
            int curFarBucket = startbucket;
            int bestFarBucket = startbucket;

            bool foundAnswer = false;
            int bins = this.UnconfTxs.Count;

            // Start counting from highest(default) or lowest feerate transactions
            for (int bucket = startbucket; bucket >= 0 && bucket <= maxbucketindex; bucket += step)
            {
                curFarBucket = bucket;
                nConf += this.ConfAvg[confTarget - 1][bucket];
                totalNum += this.TxCtAvg[bucket];
                for (int confct = confTarget; confct < this.GetMaxConfirms(); confct++)
                    extraNum += this.UnconfTxs[(nBlockHeight - confct) % bins][bucket];
                extraNum += this.OldUnconfTxs[bucket];
                // If we have enough transaction data points in this range of buckets,
                // we can test for success
                // (Only count the confirmed data points, so that each confirmation count
                // will be looking at the same amount of data and same bucket breaks)
                if (totalNum >= sufficientTxVal / (1 - this.Decay))
                {
                    double curPct = nConf / (totalNum + extraNum);

                    // Check to see if we are no longer getting confirmed at the success rate
                    if (requireGreater && curPct < successBreakPoint)
                        break;
                    if (!requireGreater && curPct > successBreakPoint)
                        break;

                    // Otherwise update the cumulative stats, and the bucket variables
                    // and reset the counters
                    foundAnswer = true;
                    nConf = 0;
                    totalNum = 0;
                    extraNum = 0;
                    bestNearBucket = curNearBucket;
                    bestFarBucket = curFarBucket;
                    curNearBucket = bucket + step;
                }
            }

            double median = -1;
            double txSum = 0;

            // Calculate the "average" feerate of the best bucket range that met success conditions
            // Find the bucket with the median transaction and then report the average feerate from that bucket
            // This is a compromise between finding the median which we can't since we don't save all tx's
            // and reporting the average which is less accurate
            int minBucket = bestNearBucket < bestFarBucket ? bestNearBucket : bestFarBucket;
            int maxBucket = bestNearBucket > bestFarBucket ? bestNearBucket : bestFarBucket;
            for (int j = minBucket; j <= maxBucket; j++)
                txSum += this.TxCtAvg[j];
            if (foundAnswer && txSum != 0)
            {
                txSum = txSum / 2;
                for (int j = minBucket; j <= maxBucket; j++)
                    if (this.TxCtAvg[j] < txSum)
                    {
                        txSum -= this.TxCtAvg[j];
                    }
                    else
                    {
                        // we're in the right bucket
                        median = this.Avg[j] / this.TxCtAvg[j];
                        break;
                    }
            }

            this.logger.LogInformation(
                $"{confTarget}: For conf success {(requireGreater ? $">" : $"<")} {successBreakPoint} need feerate {(requireGreater ? $">" : $"<")}: {median} from buckets {this.Buckets[minBucket]} -{this.Buckets[maxBucket]}  Cur Bucket stats {100 * nConf / (totalNum + extraNum)}  {nConf}/({totalNum}+{extraNum} mempool)");

            return median;
        }

        /// <summary>
        /// Return the max number of confirms we're tracking.
        /// </summary>
        /// <returns>The max number of confirms.</returns>
        public int GetMaxConfirms()
        {
            return this.ConfAvg.Count;
        }

        public void ResetLogger(ILogger logger)
        {
            this.logger = logger;
        }
    }
}
