using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Terminal;
using Zillo.Application.Services;
using Zillo.Application.DTOs;
using Zillo.Domain.Interfaces;
using Zillo.Domain.Entities;

namespace Zillo.Dashboard.Server.Controllers;

[ApiController]
[Route("api")]
public class TerminalController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ITransactionService _transactionService;
    private readonly IPaymentService _paymentService;
    private readonly ICardRepository _cardRepository;
    private readonly IUnclaimedTransactionRepository _unclaimedTransactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<TerminalController> _logger;
    private readonly IConfiguration _configuration;

    public TerminalController(
        ICustomerService customerService,
        ITransactionService transactionService,
        IPaymentService paymentService,
        ICardRepository cardRepository,
        IUnclaimedTransactionRepository unclaimedTransactionRepository,
        IAccountRepository accountRepository,
        ILogger<TerminalController> logger,
        IConfiguration configuration)
    {
        _customerService = customerService;
        _transactionService = transactionService;
        _paymentService = paymentService;
        _cardRepository = cardRepository;
        _unclaimedTransactionRepository = unclaimedTransactionRepository;
        _accountRepository = accountRepository;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generate a connection token for Stripe Terminal SDK
    /// </summary>
    [HttpPost("connection_token")]
    public async Task<IActionResult> CreateConnectionToken()
    {
        try
        {
            var options = new ConnectionTokenCreateOptions();
            var service = new ConnectionTokenService();
            var connectionToken = await service.CreateAsync(options);

            _logger.LogInformation("Created connection token successfully");

            return Ok(new { secret = connectionToken.Secret });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating connection token");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating connection token");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get account configuration for terminal (loyalty settings, branding, etc.)
    /// Uses X-Terminal-Api-Key header for authentication
    /// </summary>
    [HttpGet("terminal/config")]
    public async Task<IActionResult> GetTerminalConfig()
    {
        try
        {
            // Get terminal API key from header
            if (!Request.Headers.TryGetValue("X-Terminal-Api-Key", out var apiKeyHeader))
            {
                _logger.LogWarning("Missing X-Terminal-Api-Key header");
                return Unauthorized(new { error = "Terminal API key required" });
            }

            var terminalApiKey = apiKeyHeader.ToString();

            // Validate API key format (should start with term_sk_)
            if (string.IsNullOrEmpty(terminalApiKey) || !terminalApiKey.StartsWith("term_sk_"))
            {
                _logger.LogWarning("Invalid terminal API key format");
                return Unauthorized(new { error = "Invalid terminal API key" });
            }

            // For now, we'll use a simple approach: get the first account (single-tenant)
            // In production, you'd want to store terminal API keys in the database linked to accounts
            var accounts = await _accountRepository.GetAllAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
            {
                _logger.LogWarning("No account found");
                return NotFound(new { error = "Account not found" });
            }

            _logger.LogInformation("Terminal config fetched for account {AccountId}", account.Id);

            return Ok(new
            {
                loyaltySystemType = account.LoyaltySystemType,
                cashbackRate = account.CashbackRate,
                pointsRate = account.PointsRate,
                historicalRewardDays = account.HistoricalRewardDays,
                welcomeIncentive = account.WelcomeIncentive,
                companyName = account.CompanyName,
                slug = account.Slug
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching terminal config");
            return StatusCode(500, new { error = "Failed to fetch terminal configuration" });
        }
    }

    /// <summary>
    /// Create a PaymentIntent for terminal payment and track in payments table
    /// Requires terminal authentication via X-Terminal-API-Key header
    /// </summary>
    [HttpPost("create_payment_intent")]
    public async Task<IActionResult> CreatePaymentIntent([FromForm] Dictionary<string, string> parameters)
    {
        try
        {
            // Get authenticated terminal info from HttpContext.Items (set by middleware)
            var accountId = (Guid)HttpContext.Items["AccountId"]!;
            var terminalId = (Guid)HttpContext.Items["TerminalId"]!;
            var terminalLabel = HttpContext.Items["TerminalLabel"]?.ToString() ?? "Unknown Terminal";

            var amount = long.Parse(parameters["amount"]);
            var currency = parameters.GetValueOrDefault("currency", "nzd");
            var description = parameters.GetValueOrDefault("description", "Lemonade purchase");

            _logger.LogInformation("Creating payment intent for terminal {TerminalId} ({TerminalLabel}), amount: {Amount} {Currency}",
                terminalId, terminalLabel, amount, currency);

            // Create Stripe payment intent
            var options = new PaymentIntentCreateOptions
            {
                Amount = amount,
                Currency = currency,
                Description = description,
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
                Amount: amount / 100m, // Convert cents to dollars
                Currency: currency
            );

            var payment = await _paymentService.CreatePaymentAsync(createPaymentRequest);

            _logger.LogInformation("Created payment record {PaymentId} for payment intent {PaymentIntentId}",
                payment.Id, paymentIntent.Id);

            return Ok(new
            {
                intent = paymentIntent.ClientSecret,
                payment_intent_id = paymentIntent.Id,
                payment_id = payment.Id
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
    [HttpPost("update_payment_intent")]
    public async Task<IActionResult> UpdatePaymentIntent([FromForm] Dictionary<string, string> parameters)
    {
        try
        {
            var paymentIntentId = parameters["payment_intent_id"];
            var amount = long.Parse(parameters["amount"]);

            _logger.LogInformation("Updating payment intent {PaymentIntentId} with new amount: {Amount}", paymentIntentId, amount);

            var options = new PaymentIntentUpdateOptions
            {
                Amount = amount
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.UpdateAsync(paymentIntentId, options);

            _logger.LogInformation("Updated payment intent: {PaymentIntentId}", paymentIntent.Id);

            // Update our payment record
            var payment = await _paymentService.GetPaymentByStripePaymentIntentIdAsync(paymentIntentId);
            if (payment != null)
            {
                var updateRequest = new UpdatePaymentRequest(
                    CustomerId: null,
                    StripeChargeId: null,
                    AmountCharged: amount / 100m,
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
                intent = paymentIntent.ClientSecret,
                payment_intent_id = paymentIntent.Id
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
    [HttpPost("capture_payment_intent")]
    public async Task<IActionResult> CapturePaymentIntent([FromForm] string payment_intent_id)
    {
        try
        {
            // Get authenticated terminal info from HttpContext.Items (set by middleware)
            var accountId = (Guid)HttpContext.Items["AccountId"]!;
            var terminalId = (Guid)HttpContext.Items["TerminalId"]!;

            _logger.LogInformation("Capturing payment intent: {PaymentIntentId} for terminal {TerminalId}",
                payment_intent_id, terminalId);

            var service = new PaymentIntentService();
            var paymentIntent = await service.CaptureAsync(payment_intent_id);

            // Get the payment method to extract card fingerprint
            _logger.LogInformation("PaymentIntent.PaymentMethodId: {PaymentMethodId}", paymentIntent.PaymentMethodId ?? "NULL");

            var paymentMethodService = new PaymentMethodService();
            var paymentMethod = await paymentMethodService.GetAsync(paymentIntent.PaymentMethodId);

            _logger.LogInformation("PaymentMethod retrieved: {HasPaymentMethod}, Card: {HasCard}, CardPresent: {HasCardPresent}, Type: {Type}",
                paymentMethod != null,
                paymentMethod?.Card != null,
                paymentMethod?.CardPresent != null,
                paymentMethod?.Type);

            // For card_present payments, use CardPresent property instead of Card
            var cardFingerprint = paymentMethod?.CardPresent?.Fingerprint ?? paymentMethod?.Card?.Fingerprint;
            var cardLast4 = paymentMethod?.CardPresent?.Last4 ?? paymentMethod?.Card?.Last4;
            var cardBrand = paymentMethod?.CardPresent?.Brand ?? paymentMethod?.Card?.Brand;
            var cardExpMonth = paymentMethod?.CardPresent?.ExpMonth ?? paymentMethod?.Card?.ExpMonth;
            var cardExpYear = paymentMethod?.CardPresent?.ExpYear ?? paymentMethod?.Card?.ExpYear;

            _logger.LogInformation("Card details - Fingerprint: {Fingerprint}, Last4: {Last4}, Brand: {Brand}",
                cardFingerprint ?? "NULL",
                cardLast4 ?? "NULL",
                cardBrand ?? "NULL");

            // Get account to check loyalty system type
            var account = await _accountRepository.GetByIdAsync(accountId);
            int points = 0;
            long cashbackAmountCents = 0;

            if (account.LoyaltySystemType == "cashback")
            {
                // Calculate cashback based on account rate (in dollars first, then convert to cents)
                var cashbackDollars = Math.Round((paymentIntent.Amount / 100m) * (account.CashbackRate / 100m), 2);
                cashbackAmountCents = (long)(cashbackDollars * 100);
            }
            else
            {
                // Calculate points (1 point per dollar)
                points = (int)(paymentIntent.Amount / 100);
            }

            _logger.LogInformation("Payment captured successfully. Amount: {Amount}, Points: {Points}, Cashback: {Cashback}, LoyaltyType: {LoyaltyType}, CardFingerprint: {Fingerprint}",
                paymentIntent.Amount, points, cashbackAmountCents, account.LoyaltySystemType, cardFingerprint ?? "N/A");

            // Update payment record
            var payment = await _paymentService.GetPaymentByStripePaymentIntentIdAsync(payment_intent_id);

            Guid? customerId = null;
            string? signupUrl = null;
            int? unclaimedPoints = null;

            if (!string.IsNullOrEmpty(cardFingerprint))
            {
                // Look up if this card is registered to a customer
                var card = await _cardRepository.GetByFingerprintAsync(cardFingerprint);

                if (card != null)
                {
                    // Card is registered! Award points to customer
                    customerId = card.CustomerId;
                    var customer = await _customerService.GetCustomerByIdAsync(customerId.Value, accountId);

                    if (customer != null)
                    {
                        // Update card details and last used time
                        card.CardLast4 = cardLast4;
                        card.CardBrand = cardBrand;
                        card.CardExpMonth = cardExpMonth != null ? (int)cardExpMonth : null;
                        card.CardExpYear = cardExpYear != null ? (int)cardExpYear : null;
                        card.LastUsedAt = DateTime.UtcNow;
                        card.UpdatedAt = DateTime.UtcNow;
                        await _cardRepository.UpdateAsync(card);

                        // Update payment with customer info
                        var updateRequest = new UpdatePaymentRequest(
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
                            StripePaymentIntentId: payment_intent_id
                        );

                        await _transactionService.CreateTransactionAsync(transaction, customer.AccountId);

                        _logger.LogInformation("Awarded {Points} points to customer {CustomerId}", points, customer.Id);
                    }
                }
                else
                {
                    // Card NOT registered - create unclaimed transaction with card details
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
                        StripePaymentIntentId = payment_intent_id,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unclaimedTransactionRepository.CreateAsync(unclaimedTransaction);

                    // Get total unclaimed points for this card
                    unclaimedPoints = await _unclaimedTransactionRepository.GetTotalUnclaimedPointsByFingerprintAsync(cardFingerprint, accountId);

                    // Build signup URL with card fingerprint (reuse account variable from above)
                    signupUrl = $"/signup/{account.Slug}?fingerprint={cardFingerprint}";

                    _logger.LogInformation("Created unclaimed transaction for fingerprint {Fingerprint}. Total unclaimed: {Points} points. Signup URL: {SignupUrl} (account.Slug={Slug})",
                        cardFingerprint, unclaimedPoints, signupUrl, account.Slug);

                    // Still update payment as completed, but without customer
                    if (payment != null)
                    {
                        var updateRequest = new UpdatePaymentRequest(
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
                // No card fingerprint available - just mark payment as completed
                if (payment != null)
                {
                    var updateRequest = new UpdatePaymentRequest(
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

            _logger.LogInformation("Returning capture response: card_registered={CardRegistered}, signup_url={SignupUrl}, unclaimed_points={UnclaimedPoints}",
                customerId.HasValue, signupUrl, unclaimedPoints);

            return Ok(new
            {
                intent = paymentIntent.ClientSecret,
                payment_intent_id = paymentIntent.Id,
                loyalty_points = points,
                cashback_amount = cashbackAmountCents,
                loyalty_system_type = account.LoyaltySystemType,
                customer_id = customerId,
                payment_id = payment?.Id,
                card_registered = customerId.HasValue,
                signup_url = signupUrl,
                unclaimed_points = unclaimedPoints
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
    /// Look up customer credit/points by payment intent ID
    /// Requires terminal authentication via X-Terminal-API-Key header
    /// </summary>
    [HttpPost("lookup_customer_credit")]
    public async Task<IActionResult> LookupCustomerCredit([FromForm] string? payment_intent_id = null)
    {
        try
        {
            // Get authenticated terminal info from HttpContext.Items (set by middleware)
            var accountId = (Guid)HttpContext.Items["AccountId"]!;
            var terminalId = (Guid)HttpContext.Items["TerminalId"]!;

            if (string.IsNullOrEmpty(payment_intent_id))
            {
                return BadRequest(new { error = "Payment intent ID is required" });
            }

            _logger.LogInformation("Looking up customer credit for payment intent: {PaymentIntentId} on terminal {TerminalId}",
                payment_intent_id, terminalId);

            // Retrieve payment intent from Stripe to get payment method and fingerprint
            var paymentIntentService = new PaymentIntentService();
            var paymentIntent = await paymentIntentService.GetAsync(payment_intent_id);

            if (paymentIntent == null || string.IsNullOrEmpty(paymentIntent.PaymentMethodId))
            {
                _logger.LogWarning("Payment intent {PaymentIntentId} not found or has no payment method", payment_intent_id);
                return BadRequest(new { error = "Payment intent not found or has no payment method" });
            }

            // Get payment method to extract card fingerprint
            var paymentMethodService = new PaymentMethodService();
            var paymentMethod = await paymentMethodService.GetAsync(paymentIntent.PaymentMethodId);

            // For card_present payments, use CardPresent property instead of Card
            var fingerprint = paymentMethod?.CardPresent?.Fingerprint ?? paymentMethod?.Card?.Fingerprint;

            if (string.IsNullOrEmpty(fingerprint))
            {
                _logger.LogWarning("Payment method {PaymentMethodId} has no fingerprint", paymentIntent.PaymentMethodId);
                return BadRequest(new { error = "Card fingerprint not available" });
            }

            _logger.LogInformation("Extracted fingerprint {Fingerprint} from payment method {PaymentMethodId}",
                fingerprint, paymentIntent.PaymentMethodId);

            // Get account to determine loyalty system type (needed for all paths)
            var account = await _accountRepository.GetByIdAsync(accountId);

            var card = await _cardRepository.GetByFingerprintAsync(fingerprint);

            if (card == null)
            {
                _logger.LogInformation("Card with fingerprint {Fingerprint} not registered - returning graceful response for signup flow", fingerprint);

                // Check if there are unclaimed points for this card
                var unclaimedPoints = await _unclaimedTransactionRepository.GetTotalUnclaimedPointsByFingerprintAsync(fingerprint, accountId);

                // Build signup URL with fingerprint
                var signupUrl = $"/signup/{account.Slug}?fingerprint={fingerprint}";

                _logger.LogInformation("Building signup URL: account.Slug={Slug}, fingerprint={Fingerprint}, final URL={SignupUrl}",
                    account.Slug, fingerprint, signupUrl);

                // Return success response with null customer_id to indicate unrecognized card
                return Ok(new {
                    customer_id = (string?)null,
                    card_registered = false,
                    credit_balance = 0L,
                    email = (string?)null,
                    name = (string?)null,
                    points_balance = 0,
                    cashback_balance = 0m,
                    loyalty_system_type = account?.LoyaltySystemType ?? "points",
                    unclaimed_points = unclaimedPoints,
                    signup_url = signupUrl
                });
            }

            var customer = await _customerService.GetCustomerByIdAsync(card.CustomerId, accountId);

            if (customer == null)
            {
                _logger.LogWarning("Customer {CustomerId} not found for card fingerprint {Fingerprint}", card.CustomerId, fingerprint);
                return NotFound(new { error = "Customer not found" });
            }

            _logger.LogInformation("Found customer {CustomerId} with {Points} points and ${Cashback} cashback", customer.Id, customer.PointsBalance, customer.CashbackBalance);

            return Ok(new
            {
                customer_id = customer.Id.ToString(),
                card_registered = true,
                credit_balance = account.LoyaltySystemType == "cashback"
                    ? (long)(customer.CashbackBalance * 100)
                    : customer.PointsBalance * 100,
                email = customer.Email,
                name = customer.Name,
                points_balance = customer.PointsBalance,
                cashback_balance = customer.CashbackBalance,
                loyalty_system_type = account.LoyaltySystemType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up customer credit");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Apply loyalty redemption to payment
    /// </summary>
    [HttpPost("apply_redemption")]
    public async Task<IActionResult> ApplyRedemption(
        [FromForm] string payment_intent_id,
        [FromForm] string customer_id,
        [FromForm] long credit_to_redeem)
    {
        try
        {
            _logger.LogInformation("Applying redemption of {Credit} cents to payment intent {PaymentIntentId} for customer {CustomerId}",
                credit_to_redeem, payment_intent_id, customer_id);

            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(payment_intent_id);

            var newAmount = paymentIntent.Amount - credit_to_redeem;
            if (newAmount < 0) newAmount = 0;

            var options = new PaymentIntentUpdateOptions
            {
                Amount = newAmount
            };

            paymentIntent = await service.UpdateAsync(payment_intent_id, options);

            // Update payment record with loyalty redemption
            var payment = await _paymentService.GetPaymentByStripePaymentIntentIdAsync(payment_intent_id);
            if (payment != null && Guid.TryParse(customer_id, out var customerGuid))
            {
                var updateRequest = new UpdatePaymentRequest(
                    CustomerId: customerGuid,
                    StripeChargeId: null,
                    AmountCharged: newAmount / 100m,
                    LoyaltyRedeemed: credit_to_redeem / 100m,
                    LoyaltyEarned: null,
                    Status: null,
                    PaymentMethodType: null,
                    CompletedAt: null
                );

                await _paymentService.UpdatePaymentAsync(payment.Id, updateRequest, payment.AccountId);
            }

            _logger.LogInformation("Applied redemption. New amount: {NewAmount}", newAmount);

            return Ok(new
            {
                new_amount = newAmount,
                credit_redeemed = credit_to_redeem
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

    /// <summary>
    /// Capture payment with loyalty redemption
    /// Requires terminal authentication via X-Terminal-API-Key header
    /// </summary>
    [HttpPost("capture_with_redemption")]
    public async Task<IActionResult> CaptureWithRedemption(
        [FromForm] string payment_intent_id,
        [FromForm] string customer_id,
        [FromForm] long amount_to_capture,
        [FromForm] long credit_redeemed)
    {
        try
        {
            // Get authenticated terminal info from HttpContext.Items (set by middleware)
            var accountId = (Guid)HttpContext.Items["AccountId"]!;
            var terminalId = (Guid)HttpContext.Items["TerminalId"]!;

            _logger.LogInformation("Capturing payment {PaymentIntentId} with redemption on terminal {TerminalId}. Amount: {Amount}, Credit: {Credit}",
                payment_intent_id, terminalId, amount_to_capture, credit_redeemed);

            // Capture the payment
            var service = new PaymentIntentService();
            var options = new PaymentIntentCaptureOptions
            {
                AmountToCapture = amount_to_capture
            };
            var paymentIntent = await service.CaptureAsync(payment_intent_id, options);

            // Get payment record
            var payment = await _paymentService.GetPaymentByStripePaymentIntentIdAsync(payment_intent_id);

            // Deduct loyalty points from customer account and award points for amount paid
            if (Guid.TryParse(customer_id, out var customerGuid))
            {
                var customer = await _customerService.GetCustomerByIdAsync(customerGuid, accountId);
                var account = await _accountRepository.GetByIdAsync(accountId);

                if (customer != null && payment != null && account != null)
                {
                    // Create redemption transaction if credit was redeemed
                    if (credit_redeemed > 0)
                    {
                        if (account.LoyaltySystemType == "cashback")
                        {
                            var cashbackToRedeemDollars = credit_redeemed / 100m;
                            var cashbackToRedeemCents = credit_redeemed; // credit_redeemed is already in cents
                            var redeemTransaction = new CreateTransactionRequest(
                                CustomerId: customerGuid,
                                AccountId: accountId,
                                Points: 0,
                                CashbackAmount: -cashbackToRedeemCents,
                                Amount: -cashbackToRedeemDollars,
                                Type: "cashback_redeem",
                                Description: $"Redeemed ${credit_redeemed / 100.0:F2} cashback for purchase",
                                PaymentId: payment.Id,
                                StripePaymentIntentId: payment_intent_id
                            );

                            await _transactionService.CreateTransactionAsync(redeemTransaction, customer.AccountId);
                            _logger.LogInformation("Deducted ${Cashback} cashback from customer {CustomerId}", cashbackToRedeemDollars, customerGuid);
                        }
                        else
                        {
                            var pointsToRedeem = (int)(credit_redeemed / 100);
                            var redeemTransaction = new CreateTransactionRequest(
                                CustomerId: customerGuid,
                                AccountId: accountId,
                                Points: -pointsToRedeem,
                                CashbackAmount: 0,
                                Amount: -(credit_redeemed / 100m),
                                Type: "redeem",
                                Description: $"Redeemed ${credit_redeemed / 100.0:F2} for purchase",
                                PaymentId: payment.Id,
                                StripePaymentIntentId: payment_intent_id
                            );

                            await _transactionService.CreateTransactionAsync(redeemTransaction, customer.AccountId);
                            _logger.LogInformation("Deducted {Points} points from customer {CustomerId}", pointsToRedeem, customerGuid);
                        }
                    }

                    // Award loyalty for the amount actually paid
                    if (amount_to_capture > 0)
                    {
                        if (account.LoyaltySystemType == "cashback")
                        {
                            var cashbackEarnedDollars = Math.Round((amount_to_capture / 100m) * (account.CashbackRate / 100m), 2);
                            var cashbackEarnedCents = (long)(cashbackEarnedDollars * 100);
                            var earnTransaction = new CreateTransactionRequest(
                                CustomerId: customerGuid,
                                AccountId: accountId,
                                Points: 0,
                                CashbackAmount: cashbackEarnedCents,
                                Amount: amount_to_capture / 100m,
                                Type: "cashback_earn",
                                Description: $"Purchase ${amount_to_capture / 100.0:F2}",
                                PaymentId: payment.Id,
                                StripePaymentIntentId: payment_intent_id
                            );

                            await _transactionService.CreateTransactionAsync(earnTransaction, customer.AccountId);
                            _logger.LogInformation("Awarded ${Cashback} cashback to customer {CustomerId}", cashbackEarnedDollars, customerGuid);
                        }
                        else
                        {
                            var pointsEarned = (int)(amount_to_capture / 100);
                            var earnTransaction = new CreateTransactionRequest(
                                CustomerId: customerGuid,
                                AccountId: accountId,
                                Points: pointsEarned,
                                CashbackAmount: 0,
                                Amount: amount_to_capture / 100m,
                                Type: "earn",
                                Description: $"Purchase ${amount_to_capture / 100.0:F2}",
                                PaymentId: payment.Id,
                                StripePaymentIntentId: payment_intent_id
                            );

                            await _transactionService.CreateTransactionAsync(earnTransaction, customer.AccountId);
                            _logger.LogInformation("Awarded {Points} points to customer {CustomerId}", pointsEarned, customerGuid);
                        }
                    }

                    // Update payment record as completed
                    var updateRequest = new UpdatePaymentRequest(
                        CustomerId: customerGuid,
                        StripeChargeId: paymentIntent.LatestChargeId,
                        AmountCharged: amount_to_capture / 100m,
                        LoyaltyRedeemed: credit_redeemed / 100m,
                        LoyaltyEarned: (int)(amount_to_capture / 100),
                        Status: "completed",
                        PaymentMethodType: paymentIntent.PaymentMethodTypes?.FirstOrDefault(),
                        CompletedAt: DateTime.UtcNow
                    );

                    await _paymentService.UpdatePaymentAsync(payment.Id, updateRequest, payment.AccountId);
                }
            }

            return Ok(new { success = true, payment_id = payment?.Id });
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
    /// Email receipt to customer
    /// </summary>
    [HttpPost("email_receipt")]
    public IActionResult EmailReceipt(
        [FromForm] string email,
        [FromForm] string payment_intent_id,
        [FromForm] long amount,
        [FromForm] int loyalty_points)
    {
        try
        {
            // TODO: Implement email service integration
            // For now, just log it
            _logger.LogInformation("Receipt requested for {Email}: ${Amount}, {Points} points earned",
                email, amount / 100.0, loyalty_points);

            return Ok(new { success = true, message = "Receipt email queued (not implemented)" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error emailing receipt");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get terminal branding settings for the authenticated terminal's account
    /// Requires terminal authentication via X-Terminal-API-Key header
    /// </summary>
    [HttpGet("terminal/branding")]
    public async Task<IActionResult> GetTerminalBranding()
    {
        try
        {
            // Get authenticated terminal info from HttpContext.Items (set by middleware)
            var accountId = (Guid)HttpContext.Items["AccountId"]!;
            var terminalId = (Guid)HttpContext.Items["TerminalId"]!;

            _logger.LogInformation("Terminal {TerminalId} requesting branding settings", terminalId);

            var account = await _accountRepository.GetByIdAsync(accountId);
            if (account == null)
                return NotFound(new { error = "Account not found" });

            return Ok(new
            {
                companyName = account.CompanyName,
                logoUrl = account.BrandingLogoUrl,
                backgroundColor = account.BrandingBackgroundColor,
                textColor = account.BrandingTextColor,
                buttonColor = account.BrandingButtonColor,
                buttonTextColor = account.BrandingButtonTextColor,
                headlineText = account.BrandingHeadlineText,
                subheadlineText = account.BrandingSubheadlineText,
                qrHeadlineText = account.BrandingQrHeadlineText,
                qrSubheadlineText = account.BrandingQrSubheadlineText,
                qrButtonText = account.BrandingQrButtonText,
                recognizedHeadlineText = account.BrandingRecognizedHeadlineText,
                recognizedSubheadlineText = account.BrandingRecognizedSubheadlineText,
                recognizedButtonText = account.BrandingRecognizedButtonText,
                recognizedLinkText = account.BrandingRecognizedLinkText,
                signupUrl = $"/signup/{account.Slug}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting terminal branding");
            return StatusCode(500, new { error = ex.Message });
        }
    }

}
