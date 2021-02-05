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
        private readonly DepositService _depositService;
        private readonly WalletService _walletService;

        public WalletsController(DepositService depositService, WalletService walletService)
        {
            _depositService = depositService;
            _walletService = walletService;
        }

        [HttpGet("")]
        public async Task<IEnumerable<WalletData>> ListWallets()
        {
            return await _walletService.GetWallets(new WalletQuery());
        }

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