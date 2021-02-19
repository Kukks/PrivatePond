using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PrivatePond.Data;

namespace PrivatePond.Controllers
{
    [Route("api/v1/wallets")]
    public class WalletsController : ControllerBase
    {
        private readonly WalletService _walletService;

        public WalletsController(WalletService walletService)
        {
            _walletService = walletService;
        }

        /// <summary>
        /// gets a list of all wallets registered in the system
        /// </summary>
        /// <returns></returns>
        [HttpGet("")]
        public async Task<IEnumerable<WalletData>> ListWallets(WalletQuery query)
        {
            return await _walletService.GetWallets(query);
        }

        /// <summary>
        /// Get a specific wallet
        /// </summary>
        /// <param name="walletId">the wallet to fetch</param>
        /// <returns></returns>
        [HttpGet("{walletId}")]
        public async Task<ActionResult<WalletData>> GetWallet(string walletId)
        {
            var result = (await _walletService.GetWallets(new WalletQuery()
            {
                Ids = new[] {walletId}
            })).FirstOrDefault();
            if (result is null)
            {
                return NotFound();
            }

            return result;
        }

        /// <summary>
        /// Fetch deposits made to a wallet
        /// </summary>
        /// <param name="walletId">the wallet to fetch transactions for</param>
        /// <param name="skip">Skip x records (for paging)</param>
        /// <param name="take">Take x records (for paging)</param>
        /// <returns></returns>
        [HttpGet("{walletId}/transactions")]
        public async Task<ActionResult<List<WalletTransaction>>> GetWalletTransactions(string walletId, int skip = 0,
            int take = int.MaxValue)
        {
            var txs = await _walletService.GetWalletTransactions(new WalletTransactionQuery()
            {
                WalletIds = new[] {walletId},
                Skip = skip,
                Take = take
            }, CancellationToken.None);
            return txs;
        }

        /// <summary>
        /// Approve a deposit to an inactive request.
        /// </summary>
        /// <remarks> If a deposit is made to an inactive request, it may have been done to a wallet that is no longer accessible. These deposits are not managed actively by the system and will require manual intervention. Thus, you should only approve if you are sure the funds can be managed and you should heavily encourage users to always generate a fresh deposit request instead of reusing old ones.</remarks>
        /// <param name="walletId">the wallet id</param>
        /// <param name="walletTransactionId">the wallet transaction id to approve</param>
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [HttpPost("{walletId}/transactions/{walletTransactionId}/approve")]
        public async Task<ActionResult> ApproveWalletTransaction(string walletId, string walletTransactionId)
        {
            var txs = await _walletService.GetWalletTransactions(new WalletTransactionQuery()
            {
                WalletIds = new[] {walletId},
                Ids = new[] {walletTransactionId},
                Statuses = new[] {WalletTransaction.WalletTransactionStatus.RequiresApproval},
            }, CancellationToken.None);
            txs.ForEach(transaction => transaction.Status = WalletTransaction.WalletTransactionStatus.Confirmed);
            await _walletService.Update(new WalletService.UpdateContext()
            {
                UpdatedWalletTransactions = txs
            }, CancellationToken.None);
            if (txs.Any())
            {
                return Ok();
            }

            return NotFound();
        }
    }
}