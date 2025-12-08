using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Zillo.Application.Services;
using Zillo.Application.DTOs;
using Zillo.Domain.Interfaces;
using Zillo.Domain.Entities;

namespace Zillo.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
public class PaymentsController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ITransactionService _transactionService;
    private readonly IPaymentService _paymentService;
    private readonly ICardRepository _cardRepository;
    private readonly IUnclaimedTransactionRepository _unclaimedTransactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        ICustomerService customerService,
        ITransactionService transactionService,
        IPaymentService paymentService,
        ICardRepository cardRepository,
        IUnclaimedTransactionRepository unclaimedTransactionRepository,
        IAccountRepository accountRepository,
        ILogger<PaymentsController> logger)
    {
        _customerService = customerService;
        _transactionService = transactionService;
        _paymentService = paymentService;
        _cardRepository = cardRepository;
        _unclaimedTransactionRepository = unclaimedTransactionRepository;
        _accountRepository = accountRepository;
        _logger = logger;
    }

    /// <summary>
    /// Create a PaymentIntent for terminal payment and track in payments table
    /// Requires terminal authentication via X-Terminal-API-Key header
    /// </summary>
    [HttpPost("intents")]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
    {
        try
        {
            // Get authenticated terminal info from HttpContext.Items (set by middleware)
            var accountId = (Guid)HttpContext.Items["AccountId"]!;
            var terminalId = (Guid)HttpContext.Items["TerminalId"]!;
            var terminalLabel = HttpContext.Items["TerminalLabel"]?.ToString() ?? "Unknown Terminal";

            _logger.LogInformation("Creating payment intent for terminal {TerminalId} ({TerminalLabel}), amount: {Amount} {Currency}",
                terminalId, terminalLabel, request.Amount, request.Currency);

            // Create Stripe payment intent
            var options = new PaymentIntentCreateOptions
            {
                Amount = request.Amount,
                Currency = request.Currency ?? "nzd",
                Description = request.Description ?? "Zillo purchase",
                PaymentMethodTypes = new List<string> { "card_present" },
                CaptureMethod = "manual" // Manual capture for loyalty redemption flow
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options);

            _logger.LogInformation("Created payment intent: {PaymentIntentId}", paymentIntent.Id);

            // Create payment record in our database
            var createPaymentRequest = new CreatePaymentRequest(
                AccountId: accountId,
                CustomerId: null, // Customer not yet known at creation time
                StripePaymentIntentId: paymentIntent.Id,
                TerminalId: terminalId.ToString(),
                TerminalLabel: terminalLabel,
                Amount: request.Amount / 100m, // Convert cents to dollars
                Currency: request.Currency ?? "nzd"
            );

            var payment = await _paymentService.CreatePaymentAsync(createPaymentRequest);

            _logger.LogInformation("Created payment record {PaymentId} for payment intent {PaymentIntentId}",
                payment.Id, paymentIntent.Id);

            return Ok(new
            {
                clientSecret = paymentIntent.ClientSecret,
                paymentIntentId = paymentIntent.Id,
                paymentId = payment.Id
            });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating payment intent");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment intent");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing PaymentIntent
    /// </summary>
    [HttpPatch("intents/{paymentIntentId}")]
    public async Task<IActionResult> UpdatePaymentIntent(string paymentIntentId, [FromBody] UpdatePaymentIntentRequest request)
    {
        try
        {
            _logger.LogInformation("Updating payment intent {PaymentIntentId} with new amount: {Amount}", paymentIntentId, request.Amount);

            var options = new PaymentIntentUpdateOptions
            {
                Amount = request.Amount
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.UpdateAsync(paymentIntentId, options);

            _logger.LogInformation("Updated payment intent: {PaymentIntentId}", paymentIntent.Id);

            // Update our payment record
            var payment = await _paymentService.GetPaymentByStripePaymentIntentIdAsync(paymentIntentId);
            if (payment != null)
            {
                var updateRequest = new Application.DTOs.UpdatePaymentRequest(
                    CustomerId: null,
                    StripeChargeId: null,
                    AmountCharged: request.Amount / 100m,
                    LoyaltyRedeemed: null,
                    LoyaltyEarned: null,
                    Status: null,
                    PaymentMethodType: null,
                    CompletedAt: null
                );

                await _paymentService.UpdatePaymentAsync(payment.Id, updateRequest, payment.AccountId);
            }

            return Ok(new
            {
                clientSecret = paymentIntent.ClientSecret,
                paymentIntentId = paymentIntent.Id
            });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error updating payment intent");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment intent");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Capture a PaymentIntent and automatically link to customer via card fingerprint
    /// Requires terminal authentication via X-Terminal-API-Key header
    /// </summary>
    [HttpPost("intents/{paymentIntentId}/capture")]
    public async Task<IActionResult> CapturePaymentIntent(string paymentIntentId)
    {
        try
        {
            // Get authenticated terminal info from HttpContext.Items (set by middleware)
            var accountId = (Guid)HttpContext.Items["AccountId"]!;
            var terminalId = (Guid)HttpContext.Items["TerminalId"]!;

            _logger.LogInformation("Capturing payment intent: {PaymentIntentId} for terminal {TerminalId}",
                paymentIntentId, terminalId);

            var service = new PaymentIntentService();
            var paymentIntent = await service.CaptureAsync(paymentIntentId);

            // Get the payment method to extract card fingerprint
            var paymentMethodService = new PaymentMethodService();
            var paymentMethod = await paymentMethodService.GetAsync(paymentIntent.PaymentMethodId);

            // For card_present payments, use CardPresent property instead of Card
            var cardFingerprint = paymentMethod?.CardPresent?.Fingerprint ?? paymentMethod?.Card?.Fingerprint;
            var cardLast4 = paymentMethod?.CardPresent?.Last4 ?? paymentMethod?.Card?.Last4;
            var cardBrand = paymentMethod?.CardPresent?.Brand ?? paymentMethod?.Card?.Brand;
            var cardExpMonth = paymentMethod?.CardPresent?.ExpMonth ?? paymentMethod?.Card?.ExpMonth;
            var cardExpYear = paymentMethod?.CardPresent?.ExpYear ?? paymentMethod?.Card?.ExpYear;

            // Get account to check loyalty system type
            var account = await _accountRepository.GetByIdAsync(accountId);
            int points = 0;
            long cashbackAmountCents = 0;

            if (account.LoyaltySystemType == "cashback")
            {
                var cashbackDollars = Math.Round((paymentIntent.Amount / 100m) * (account.CashbackRate / 100m), 2);
                cashbackAmountCents = (long)(cashbackDollars * 100);
            }
            else
            {
                points = (int)(paymentIntent.Amount / 100);
            }

            // Update payment record
            var payment = await _paymentService.GetPaymentByStripePaymentIntentIdAsync(paymentIntentId);

            Guid? customerId = null;
            string? signupUrl = null;
            int? unclaimedPoints = null;

            if (!string.IsNullOrEmpty(cardFingerprint))
            {
                var card = await _cardRepository.GetByFingerprintAsync(cardFingerprint);

                if (card != null)
                {
                    customerId = card.CustomerId;
                    var customer = await _customerService.GetCustomerByIdAsync(customerId.Value, accountId);

                    if (customer != null)
                    {
                        // Update card details
                        card.CardLast4 = cardLast4;
                        card.CardBrand = cardBrand;
                        card.CardExpMonth = cardExpMonth != null ? (int)cardExpMonth : null;
                        card.CardExpYear = cardExpYear != null ? (int)cardExpYear : null;
                        card.LastUsedAt = DateTime.UtcNow;
                        card.UpdatedAt = DateTime.UtcNow;
                        await _cardRepository.UpdateAsync(card);

                        // Update payment with customer info
                        var updateRequest = new Application.DTOs.UpdatePaymentRequest(
                            CustomerId: customer.Id,
                            StripeChargeId: paymentIntent.LatestChargeId,
                            AmountCharged: paymentIntent.Amount / 100m,
                            LoyaltyRedeemed: 0,
                            LoyaltyEarned: points,
                            Status: "completed",
                            PaymentMethodType: paymentIntent.PaymentMethodTypes?.FirstOrDefault(),
                            CompletedAt: DateTime.UtcNow
                        );

                        await _paymentService.UpdatePaymentAsync(payment.Id, updateRequest, payment.AccountId);

                        // Create transaction record
                        var transactionType = account.LoyaltySystemType == "cashback" ? "cashback_earn" : "earn";
                        var transaction = new CreateTransactionRequest(
                            CustomerId: customer.Id,
                            AccountId: accountId,
                            Points: points,
                            CashbackAmount: cashbackAmountCents,
                            Amount: paymentIntent.Amount / 100m,
                            Type: transactionType,
                            Description: $"Purchase ${paymentIntent.Amount / 100.0:F2}",
                            PaymentId: payment.Id,
                            StripePaymentIntentId: paymentIntentId
                        );

                        await _transactionService.CreateTransactionAsync(transaction, customer.AccountId);
                    }
                }
                else
                {
                    // Card NOT registered - create unclaimed transaction
                    var unclaimedTransaction = new UnclaimedTransaction
                    {
                        Id = Guid.NewGuid(),
                        AccountId = accountId,
                        CardFingerprint = cardFingerprint,
                        CardLast4 = cardLast4,
                        CardBrand = cardBrand,
                        CardExpMonth = cardExpMonth != null ? (int)cardExpMonth : null,
                        CardExpYear = cardExpYear != null ? (int)cardExpYear : null,
                        Points = points,
                        CashbackAmount = cashbackAmountCents,
                        Amount = paymentIntent.Amount / 100m,
                        Description = $"Purchase ${paymentIntent.Amount / 100.0:F2}",
                        PaymentId = payment?.Id,
                        StripePaymentIntentId = paymentIntentId,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unclaimedTransactionRepository.CreateAsync(unclaimedTransaction);
                    unclaimedPoints = await _unclaimedTransactionRepository.GetTotalUnclaimedPointsByFingerprintAsync(cardFingerprint, accountId);
                    signupUrl = $"/signup/{account.Slug}?fingerprint={cardFingerprint}";

                    if (payment != null)
                    {
                        var updateRequest = new Application.DTOs.UpdatePaymentRequest(
                            CustomerId: null,
                            StripeChargeId: paymentIntent.LatestChargeId,
                            AmountCharged: paymentIntent.Amount / 100m,
                            LoyaltyRedeemed: 0,
                            LoyaltyEarned: points,
                            Status: "completed",
                            PaymentMethodType: paymentIntent.PaymentMethodTypes?.FirstOrDefault(),
                            CompletedAt: DateTime.UtcNow
                        );

                        await _paymentService.UpdatePaymentAsync(payment.Id, updateRequest, payment.AccountId);
                    }
                }
            }
            else
            {
                if (payment != null)
                {
                    var updateRequest = new Application.DTOs.UpdatePaymentRequest(
                        CustomerId: null,
                        StripeChargeId: paymentIntent.LatestChargeId,
                        AmountCharged: paymentIntent.Amount / 100m,
                        LoyaltyRedeemed: 0,
                        LoyaltyEarned: points,
                        Status: "completed",
                        PaymentMethodType: paymentIntent.PaymentMethodTypes?.FirstOrDefault(),
                        CompletedAt: DateTime.UtcNow
                    );

                    await _paymentService.UpdatePaymentAsync(payment.Id, updateRequest, payment.AccountId);
                }
            }

            return Ok(new
            {
                clientSecret = paymentIntent.ClientSecret,
                paymentIntentId = paymentIntent.Id,
                loyaltyPoints = points,
                cashbackAmount = cashbackAmountCents,
                loyaltySystemType = account.LoyaltySystemType,
                customerId = customerId,
                paymentId = payment?.Id,
                cardRegistered = customerId.HasValue,
                signupUrl = signupUrl,
                unclaimedPoints = unclaimedPoints
            });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error capturing payment intent");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing payment intent");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Capture payment with loyalty redemption
    /// Requires terminal authentication via X-Terminal-API-Key header
    /// </summary>
    [HttpPost("intents/{paymentIntentId}/capture-with-redemption")]
    public async Task<IActionResult> CaptureWithRedemption(
        string paymentIntentId,
        [FromBody] CaptureWithRedemptionRequest request)
    {
        try
        {
            var accountId = (Guid)HttpContext.Items["AccountId"]!;
            var terminalId = (Guid)HttpContext.Items["TerminalId"]!;

            _logger.LogInformation("Capturing payment {PaymentIntentId} with redemption on terminal {TerminalId}. Amount: {Amount}, Credit: {Credit}",
                paymentIntentId, terminalId, request.AmountToCapture, request.CreditRedeemed);

            var service = new PaymentIntentService();
            var options = new PaymentIntentCaptureOptions
            {
                AmountToCapture = request.AmountToCapture
            };
            var paymentIntent = await service.CaptureAsync(paymentIntentId, options);

            var payment = await _paymentService.GetPaymentByStripePaymentIntentIdAsync(paymentIntentId);

            if (Guid.TryParse(request.CustomerId, out var customerGuid))
            {
                var customer = await _customerService.GetCustomerByIdAsync(customerGuid, accountId);
                var account = await _accountRepository.GetByIdAsync(accountId);

                if (customer != null && payment != null && account != null)
                {
                    // Create redemption transaction if credit was redeemed
                    if (request.CreditRedeemed > 0)
                    {
                        if (account.LoyaltySystemType == "cashback")
                        {
                            var cashbackToRedeemDollars = request.CreditRedeemed / 100m;
                            var redeemTransaction = new CreateTransactionRequest(
                                CustomerId: customerGuid,
                                AccountId: accountId,
                                Points: 0,
                                CashbackAmount: -request.CreditRedeemed,
                                Amount: -cashbackToRedeemDollars,
                                Type: "cashback_redeem",
                                Description: $"Redeemed ${request.CreditRedeemed / 100.0:F2} cashback for purchase",
                                PaymentId: payment.Id,
                                StripePaymentIntentId: paymentIntentId
                            );

                            await _transactionService.CreateTransactionAsync(redeemTransaction, customer.AccountId);
                        }
                        else
                        {
                            var pointsToRedeem = (int)(request.CreditRedeemed / 100);
                            var redeemTransaction = new CreateTransactionRequest(
                                CustomerId: customerGuid,
                                AccountId: accountId,
                                Points: -pointsToRedeem,
                                CashbackAmount: 0,
                                Amount: -(request.CreditRedeemed / 100m),
                                Type: "redeem",
                                Description: $"Redeemed ${request.CreditRedeemed / 100.0:F2} for purchase",
                                PaymentId: payment.Id,
                                StripePaymentIntentId: paymentIntentId
                            );

                            await _transactionService.CreateTransactionAsync(redeemTransaction, customer.AccountId);
                        }
                    }

                    // Award loyalty for the amount actually paid
                    if (request.AmountToCapture > 0)
                    {
                        if (account.LoyaltySystemType == "cashback")
                        {
                            var cashbackEarnedDollars = Math.Round((request.AmountToCapture / 100m) * (account.CashbackRate / 100m), 2);
                            var cashbackEarnedCents = (long)(cashbackEarnedDollars * 100);
                            var earnTransaction = new CreateTransactionRequest(
                                CustomerId: customerGuid,
                                AccountId: accountId,
                                Points: 0,
                                CashbackAmount: cashbackEarnedCents,
                                Amount: request.AmountToCapture / 100m,
                                Type: "cashback_earn",
                                Description: $"Purchase ${request.AmountToCapture / 100.0:F2}",
                                PaymentId: payment.Id,
                                StripePaymentIntentId: paymentIntentId
                            );

                            await _transactionService.CreateTransactionAsync(earnTransaction, customer.AccountId);
                        }
                        else
                        {
                            var pointsEarned = (int)(request.AmountToCapture / 100);
                            var earnTransaction = new CreateTransactionRequest(
                                CustomerId: customerGuid,
                                AccountId: accountId,
                                Points: pointsEarned,
                                CashbackAmount: 0,
                                Amount: request.AmountToCapture / 100m,
                                Type: "earn",
                                Description: $"Purchase ${request.AmountToCapture / 100.0:F2}",
                                PaymentId: payment.Id,
                                StripePaymentIntentId: paymentIntentId
                            );

                            await _transactionService.CreateTransactionAsync(earnTransaction, customer.AccountId);
                        }
                    }

                    // Update payment record as completed
                    var updateRequest = new Application.DTOs.UpdatePaymentRequest(
                        CustomerId: customerGuid,
                        StripeChargeId: paymentIntent.LatestChargeId,
                        AmountCharged: request.AmountToCapture / 100m,
                        LoyaltyRedeemed: request.CreditRedeemed / 100m,
                        LoyaltyEarned: (int)(request.AmountToCapture / 100),
                        Status: "completed",
                        PaymentMethodType: paymentIntent.PaymentMethodTypes?.FirstOrDefault(),
                        CompletedAt: DateTime.UtcNow
                    );

                    await _paymentService.UpdatePaymentAsync(payment.Id, updateRequest, payment.AccountId);
                }
            }

            return Ok(new { success = true, paymentId = payment?.Id });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error capturing with redemption");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing with redemption");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Apply loyalty redemption to payment
    /// </summary>
    [HttpPost("intents/{paymentIntentId}/apply-redemption")]
    public async Task<IActionResult> ApplyRedemption(string paymentIntentId, [FromBody] ApplyRedemptionRequest request)
    {
        try
        {
            _logger.LogInformation("Applying redemption of {Credit} cents to payment intent {PaymentIntentId} for customer {CustomerId}",
                request.CreditToRedeem, paymentIntentId, request.CustomerId);

            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId);

            var newAmount = paymentIntent.Amount - request.CreditToRedeem;
            if (newAmount < 0) newAmount = 0;

            var options = new PaymentIntentUpdateOptions
            {
                Amount = newAmount
            };

            paymentIntent = await service.UpdateAsync(paymentIntentId, options);

            // Update payment record with loyalty redemption
            var payment = await _paymentService.GetPaymentByStripePaymentIntentIdAsync(paymentIntentId);
            if (payment != null && Guid.TryParse(request.CustomerId, out var customerGuid))
            {
                var updateRequest = new Application.DTOs.UpdatePaymentRequest(
                    CustomerId: customerGuid,
                    StripeChargeId: null,
                    AmountCharged: newAmount / 100m,
                    LoyaltyRedeemed: request.CreditToRedeem / 100m,
                    LoyaltyEarned: null,
                    Status: null,
                    PaymentMethodType: null,
                    CompletedAt: null
                );

                await _paymentService.UpdatePaymentAsync(payment.Id, updateRequest, payment.AccountId);
            }

            return Ok(new
            {
                newAmount = newAmount,
                creditRedeemed = request.CreditToRedeem
            });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error applying redemption");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying redemption");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

// Request DTOs
public record CreatePaymentIntentRequest(long Amount, string? Currency = "nzd", string? Description = null);
public record UpdatePaymentIntentRequest(long Amount);
public record CaptureWithRedemptionRequest(string CustomerId, long AmountToCapture, long CreditRedeemed);
public record ApplyRedemptionRequest(string CustomerId, long CreditToRedeem);
