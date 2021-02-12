using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace PrivatePond.Controllers
{
    [Route("api/v1/transactions")]
    public class TransactionsController : ControllerBase
    {
        private readonly WalletService _walletService;

        public TransactionsController(WalletService walletService)
        {
            _walletService = walletService;
        }

        /// <summary>
        /// Get Transactions </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        [HttpGet("")]
        public async Task<ActionResult<List<TransferRequestData>>> GetTransferRequests(WalletTransactionQuery query)
        {
            return Ok(await _walletService.GetWalletTransactions(query, CancellationToken.None));
        }
    }
}