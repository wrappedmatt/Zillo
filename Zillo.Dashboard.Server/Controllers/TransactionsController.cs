using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Zillo.Dashboard.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly IAuthService _authService;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        ITransactionService transactionService,
        IAuthService authService,
        ILogger<TransactionsController> logger)
    {
        _transactionService = transactionService;
        _authService = authService;
        _logger = logger;
    }

    private async Task<Guid?> GetAccountIdFromToken()
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var token = authHeader.Substring("Bearer ".Length);
        var user = await _authService.GetCurrentUserAsync(token);
        return user?.Id;
    }

    [HttpGet("customer/{customerId}")]
    public async Task<ActionResult<IEnumerable<TransactionDto>>> GetTransactionsByCustomer(Guid customerId)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var transactions = await _transactionService.GetTransactionsByCustomerIdAsync(customerId, accountId.Value);
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transactions");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> CreateTransaction([FromBody] CreateTransactionRequest request)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var transaction = await _transactionService.CreateTransactionAsync(request, accountId.Value);
            return CreatedAtAction(nameof(GetTransactionsByCustomer), new { customerId = transaction.CustomerId }, transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transaction");
            return BadRequest(new { error = ex.Message });
        }
    }
}
