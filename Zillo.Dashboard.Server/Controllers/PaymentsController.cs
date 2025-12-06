using Zillo.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Supabase;
using Postgrest.Attributes;
using Postgrest.Models;

namespace Zillo.Dashboard.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly Client _supabase;
    private readonly IAuthService _authService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        Client supabase,
        IAuthService authService,
        ILogger<PaymentsController> logger)
    {
        _supabase = supabase;
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

    [HttpGet]
    public async Task<ActionResult> GetAllPayments()
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var response = await _supabase
                .From<PaymentModel>()
                .Select("*, customer:customers!customer_id(id, name, email)")
                .Where(x => x.AccountId == accountId.Value)
                .Order("created_at", Postgrest.Constants.Ordering.Descending)
                .Limit(1000)
                .Get();

            var payments = response.Models.Select(p => new
            {
                id = p.Id,
                stripePaymentIntentId = p.StripePaymentIntentId,
                customerId = p.CustomerId,
                customer = p.Customer,
                amount = p.Amount,
                amountCharged = p.AmountCharged,
                loyaltyEarned = p.LoyaltyEarned,
                loyaltyRedeemed = p.LoyaltyRedeemed,
                status = p.Status,
                terminalLabel = p.TerminalLabel,
                createdAt = p.CreatedAt,
                completedAt = p.CompletedAt,
                // Map to transaction-like format for frontend
                points = p.LoyaltyEarned,
                type = "earn",
                description = $"Payment {(p.Status == "completed" ? "completed" : p.Status)} - {p.TerminalLabel ?? "Terminal"}"
            });

            return Ok(payments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payments");
            return BadRequest(new { error = ex.Message });
        }
    }
}

[Table("payments")]
public class PaymentModel : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("account_id")]
    public Guid AccountId { get; set; }

    [Column("customer_id")]
    public Guid? CustomerId { get; set; }

    [Column("stripe_payment_intent_id")]
    public string StripePaymentIntentId { get; set; } = string.Empty;

    [Column("terminal_label")]
    public string? TerminalLabel { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("amount_charged")]
    public decimal AmountCharged { get; set; }

    [Column("loyalty_earned")]
    public int LoyaltyEarned { get; set; }

    [Column("loyalty_redeemed")]
    public decimal LoyaltyRedeemed { get; set; }

    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Reference(typeof(CustomerReference))]
    public CustomerReference? Customer { get; set; }
}

public class CustomerReference
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;
}
