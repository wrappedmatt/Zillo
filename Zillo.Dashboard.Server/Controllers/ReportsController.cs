using Zillo.Application.Services;
using Zillo.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Zillo.Dashboard.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnclaimedTransactionRepository _unclaimedTransactionRepository;
    private readonly IAuthService _authService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        ICustomerRepository customerRepository,
        ITransactionRepository transactionRepository,
        IUnclaimedTransactionRepository unclaimedTransactionRepository,
        IAuthService authService,
        ILogger<ReportsController> logger)
    {
        _customerRepository = customerRepository;
        _transactionRepository = transactionRepository;
        _unclaimedTransactionRepository = unclaimedTransactionRepository;
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

    [HttpGet("summary")]
    public async Task<IActionResult> GetReportsSummary()
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            // Get all customers for this account
            var customers = await _customerRepository.GetByAccountIdAsync(accountId.Value);
            var totalCustomers = customers.Count();

            // Get all transactions for this account
            var allTransactions = new List<Domain.Entities.Transaction>();
            foreach (var customer in customers)
            {
                var customerTransactions = await _transactionRepository.GetByCustomerIdAsync(customer.Id);
                allTransactions.AddRange(customerTransactions);
            }

            // Calculate points issued and redeemed
            var totalPointsIssued = allTransactions
                .Where(t => t.Type == "earn" || t.Type == "bonus")
                .Sum(t => t.Points);

            var totalPointsRedeemed = allTransactions
                .Where(t => t.Type == "redeem")
                .Sum(t => Math.Abs(t.Points));

            // Outstanding liability (1 point = $1)
            var outstandingPoints = totalPointsIssued - totalPointsRedeemed;
            var outstandingLiability = (decimal)outstandingPoints;

            // Calculate average points per customer
            var avgPointsPerCustomer = totalCustomers > 0
                ? customers.Average(c => c.PointsBalance)
                : 0;

            // Total transactions
            var totalTransactions = allTransactions.Count;

            // Get unclaimed points for this account
            var unclaimedTransactions = await _unclaimedTransactionRepository.GetUnclaimedByAccountAsync(accountId.Value);
            var unclaimedPoints = unclaimedTransactions.Sum(t => t.Points);

            // Calculate active customers (customers with transactions in last 30 days)
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var activeCustomerIds = allTransactions
                .Where(t => t.CreatedAt >= thirtyDaysAgo)
                .Select(t => t.CustomerId)
                .Distinct()
                .Count();

            var summary = new
            {
                totalCustomers,
                totalPointsIssued,
                totalPointsRedeemed,
                outstandingLiability,
                avgPointsPerCustomer,
                totalTransactions,
                unclaimedPoints,
                activeCustomers = activeCustomerIds
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating reports summary");
            return BadRequest(new { error = ex.Message });
        }
    }
}
