# Backend Integration Guide

This guide explains how to integrate the LemonadeTerminalApp with your LemonadeApp.Server backend.

## Overview

The terminal app requires specific API endpoints to handle Stripe Terminal payments and loyalty point management. You'll need to add a new `TerminalController` to your `LemonadeApp.Server` project.

## Required NuGet Packages

Add these to `LemonadeApp.Server.csproj`:

```xml
<PackageReference Include="Stripe.net" Version="45.0.0" />
```

## Configuration

### 1. Add Stripe Configuration

Edit `LemonadeApp.Server/appsettings.json`:

```json
{
  "Supabase": {
    "Url": "your-supabase-url",
    "Key": "your-supabase-key"
  },
  "Stripe": {
    "SecretKey": "sk_test_your_stripe_secret_key",
    "PublishableKey": "pk_test_your_stripe_publishable_key"
  }
}
```

### 2. Initialize Stripe in Program.cs

Add this before `var app = builder.Build();`:

```csharp
// Configure Stripe
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
```

## Required API Endpoints

### TerminalController.cs

Create a new controller at `LemonadeApp.Server/Controllers/TerminalController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Terminal;
using LemonadeApp.Application.Services;
using LemonadeApp.Application.DTOs;

namespace LemonadeApp.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TerminalController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly ITransactionService _transactionService;
        private readonly ILogger<TerminalController> _logger;

        public TerminalController(
            ICustomerService customerService,
            ITransactionService transactionService,
            ILogger<TerminalController> logger)
        {
            _customerService = customerService;
            _transactionService = transactionService;
            _logger = logger;
        }

        /// <summary>
        /// Generate a connection token for Stripe Terminal
        /// </summary>
        [HttpPost("connection_token")]
        public async Task<IActionResult> CreateConnectionToken()
        {
            try
            {
                var options = new ConnectionTokenCreateOptions();
                var service = new ConnectionTokenService();
                var connectionToken = await service.CreateAsync(options);

                return Ok(new { secret = connectionToken.Secret });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating connection token");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Create a PaymentIntent for terminal payment
        /// </summary>
        [HttpPost("create_payment_intent")]
        public async Task<IActionResult> CreatePaymentIntent([FromForm] Dictionary<string, string> parameters)
        {
            try
            {
                var amount = long.Parse(parameters["amount"]);
                var currency = parameters.GetValueOrDefault("currency", "nzd");
                var description = parameters.GetValueOrDefault("description", "Lemonade purchase");

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

                return Ok(new
                {
                    intent = paymentIntent.ClientSecret,
                    payment_intent_id = paymentIntent.Id
                });
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

                var options = new PaymentIntentUpdateOptions
                {
                    Amount = amount
                };

                var service = new PaymentIntentService();
                var paymentIntent = await service.UpdateAsync(paymentIntentId, options);

                return Ok(new
                {
                    intent = paymentIntent.ClientSecret,
                    payment_intent_id = paymentIntent.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment intent");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Capture a PaymentIntent
        /// </summary>
        [HttpPost("capture_payment_intent")]
        public async Task<IActionResult> CapturePaymentIntent([FromForm] string payment_intent_id)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.CaptureAsync(payment_intent_id);

                // Award loyalty points (1 point per dollar)
                var points = (int)(paymentIntent.Amount / 100);

                return Ok(new
                {
                    intent = paymentIntent.ClientSecret,
                    payment_intent_id = paymentIntent.Id,
                    loyalty_points = points
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing payment intent");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Look up customer credit/points by payment intent
        /// </summary>
        [HttpGet("lookup_customer_credit")]
        public async Task<IActionResult> LookupCustomerCredit([FromQuery] string payment_intent_id)
        {
            try
            {
                // Get PaymentIntent to retrieve customer info
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(payment_intent_id);

                // TODO: Implement customer lookup logic
                // For now, return mock data
                return Ok(new
                {
                    customer_id = "cust_123",
                    credit_balance = 1000, // 10.00 in cents
                    email = "customer@example.com"
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
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(payment_intent_id);

                var newAmount = paymentIntent.Amount - credit_to_redeem;
                if (newAmount < 0) newAmount = 0;

                var options = new PaymentIntentUpdateOptions
                {
                    Amount = newAmount
                };

                paymentIntent = await service.UpdateAsync(payment_intent_id, options);

                return Ok(new
                {
                    new_amount = newAmount,
                    credit_redeemed = credit_to_redeem
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying redemption");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Capture payment with loyalty redemption
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
                // Capture the payment
                var service = new PaymentIntentService();
                var options = new PaymentIntentCaptureOptions
                {
                    AmountToCapture = amount_to_capture
                };
                await service.CaptureAsync(payment_intent_id, options);

                // TODO: Deduct loyalty points from customer account
                // TODO: Record transaction in database

                return Ok(new { success = true });
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
        public async Task<IActionResult> EmailReceipt(
            [FromForm] string email,
            [FromForm] string payment_intent_id,
            [FromForm] long amount,
            [FromForm] int loyalty_points)
        {
            try
            {
                // TODO: Implement email service
                // For now, just log it
                _logger.LogInformation($"Would email receipt to {email} for ${amount / 100.0:F2} with {loyalty_points} points");

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error emailing receipt");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
```

## Integration with Existing Services

### Connecting to Customer Service

To integrate loyalty points with your existing customer database, update the `CapturePaymentIntent` endpoint:

```csharp
[HttpPost("capture_payment_intent")]
public async Task<IActionResult> CapturePaymentIntent([FromForm] string payment_intent_id, [FromForm] string? customer_email)
{
    try
    {
        var service = new PaymentIntentService();
        var paymentIntent = await service.CaptureAsync(payment_intent_id);

        // Calculate loyalty points (1 point per dollar)
        var points = (int)(paymentIntent.Amount / 100);

        // If customer email provided, award points
        if (!string.IsNullOrEmpty(customer_email))
        {
            // Find customer by email
            var customer = await _customerService.GetCustomerByEmailAsync(customer_email);

            if (customer != null)
            {
                // Create transaction record
                var transaction = new CreateTransactionDto
                {
                    CustomerId = customer.Id,
                    Amount = points,
                    Type = "earn",
                    Description = $"Purchase ${paymentIntent.Amount / 100.0:F2}"
                };

                await _transactionService.CreateTransactionAsync(transaction);
            }
        }

        return Ok(new
        {
            intent = paymentIntent.ClientSecret,
            payment_intent_id = paymentIntent.Id,
            loyalty_points = points
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error capturing payment intent");
        return StatusCode(500, new { error = ex.Message });
    }
}
```

## Testing the Integration

### 1. Start the Backend

```bash
cd LemonadeApp.Server
dotnet run
```

### 2. Test Connection Token Endpoint

```bash
curl -X POST http://localhost:7024/api/terminal/connection_token
```

Expected response:
```json
{
  "secret": "pst_test_..."
}
```

### 3. Test Create Payment Intent

```bash
curl -X POST http://localhost:7024/api/terminal/create_payment_intent \
  -d "amount=1000" \
  -d "currency=nzd"
```

Expected response:
```json
{
  "intent": "pi_..._secret_...",
  "payment_intent_id": "pi_..."
}
```

## CORS Configuration

If testing from the terminal app, ensure CORS is configured in `Program.cs`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowTerminal",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// After app.Build():
app.UseCors("AllowTerminal");
```

## Security Considerations

1. **API Key Protection**
   - Never commit `appsettings.json` with real keys
   - Use environment variables in production
   - Rotate keys immediately if exposed

2. **Authentication**
   - Consider adding API key authentication for terminal endpoints
   - Use HTTPS in production
   - Validate all input parameters

3. **Rate Limiting**
   - Implement rate limiting to prevent abuse
   - Monitor for unusual patterns

## Next Steps

1. ✅ Add `TerminalController` to your backend
2. ✅ Configure Stripe API keys
3. ✅ Test endpoints with curl or Postman
4. ✅ Update terminal app's `local.properties` with backend URL
5. ✅ Test end-to-end payment flow
6. ⬜ Integrate with existing Customer/Transaction services
7. ⬜ Implement email receipt functionality
8. ⬜ Add proper error handling and logging
9. ⬜ Deploy to production

## Troubleshooting

### Stripe API Errors

**Problem**: "No API key provided"
- Verify `appsettings.json` has correct `Stripe:SecretKey`
- Ensure `Stripe.StripeConfiguration.ApiKey` is set in `Program.cs`

**Problem**: "Invalid currency"
- Check your Stripe account country settings
- Update currency parameter in terminal app

### CORS Errors

**Problem**: "Access-Control-Allow-Origin" error in browser
- Add CORS policy as shown above
- Ensure policy is applied before routing

### Connection Token Fails

**Problem**: Terminal app can't connect
- Verify Stripe Terminal is enabled in your account
- Check API key has Terminal permissions
- Review network logs for specific error

## Resources

- [Stripe.NET SDK Documentation](https://github.com/stripe/stripe-dotnet)
- [Stripe Terminal API Reference](https://stripe.com/docs/api/terminal)
- [Stripe Payment Intents Guide](https://stripe.com/docs/payments/payment-intents)
