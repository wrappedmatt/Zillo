using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Zillo.Application.Services;
using Zillo.Domain.Interfaces;

namespace Zillo.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ICardRepository _cardRepository;
    private readonly IUnclaimedTransactionRepository _unclaimedTransactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        ICustomerService customerService,
        ICardRepository cardRepository,
        IUnclaimedTransactionRepository unclaimedTransactionRepository,
        IAccountRepository accountRepository,
        ILogger<CustomersController> logger)
    {
        _customerService = customerService;
        _cardRepository = cardRepository;
        _unclaimedTransactionRepository = unclaimedTransactionRepository;
        _accountRepository = accountRepository;
        _logger = logger;
    }

    /// <summary>
    /// Look up customer credit/points by payment intent ID
    /// Requires terminal authentication via X-Terminal-API-Key header
    /// </summary>
    [HttpPost("lookup-by-payment")]
    public async Task<IActionResult> LookupCustomerCredit([FromBody] LookupCustomerRequest request)
    {
        try
        {
            var accountId = (Guid)HttpContext.Items["AccountId"]!;
            var terminalId = (Guid)HttpContext.Items["TerminalId"]!;

            if (string.IsNullOrEmpty(request.PaymentIntentId))
            {
                return BadRequest(new { error = "Payment intent ID is required" });
            }

            _logger.LogInformation("Looking up customer credit for payment intent: {PaymentIntentId} on terminal {TerminalId}",
                request.PaymentIntentId, terminalId);

            // Retrieve payment intent from Stripe to get payment method and fingerprint
            var paymentIntentService = new PaymentIntentService();
            var paymentIntent = await paymentIntentService.GetAsync(request.PaymentIntentId);

            if (paymentIntent == null || string.IsNullOrEmpty(paymentIntent.PaymentMethodId))
            {
                return BadRequest(new { error = "Payment intent not found or has no payment method" });
            }

            // Get payment method to extract card fingerprint
            var paymentMethodService = new PaymentMethodService();
            var paymentMethod = await paymentMethodService.GetAsync(paymentIntent.PaymentMethodId);

            var fingerprint = paymentMethod?.CardPresent?.Fingerprint ?? paymentMethod?.Card?.Fingerprint;

            if (string.IsNullOrEmpty(fingerprint))
            {
                return BadRequest(new { error = "Card fingerprint not available" });
            }

            var account = await _accountRepository.GetByIdAsync(accountId);
            var card = await _cardRepository.GetByFingerprintAsync(fingerprint);

            if (card == null)
            {
                var unclaimedPoints = await _unclaimedTransactionRepository.GetTotalUnclaimedPointsByFingerprintAsync(fingerprint, accountId);
                var signupUrl = $"/signup/{account.Slug}?fingerprint={fingerprint}";

                return Ok(new
                {
                    customerId = (string?)null,
                    cardRegistered = false,
                    creditBalance = 0L,
                    email = (string?)null,
                    name = (string?)null,
                    pointsBalance = 0,
                    cashbackBalance = 0m,
                    loyaltySystemType = account?.LoyaltySystemType ?? "points",
                    unclaimedPoints = unclaimedPoints,
                    signupUrl = signupUrl
                });
            }

            var customer = await _customerService.GetCustomerByIdAsync(card.CustomerId, accountId);

            if (customer == null)
            {
                return NotFound(new { error = "Customer not found" });
            }

            return Ok(new
            {
                customerId = customer.Id.ToString(),
                cardRegistered = true,
                creditBalance = account.LoyaltySystemType == "cashback"
                    ? (long)(customer.CashbackBalance * 100)
                    : customer.PointsBalance * 100,
                email = customer.Email,
                name = customer.Name,
                pointsBalance = customer.PointsBalance,
                cashbackBalance = customer.CashbackBalance,
                loyaltySystemType = account.LoyaltySystemType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up customer credit");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public record LookupCustomerRequest(string PaymentIntentId);
