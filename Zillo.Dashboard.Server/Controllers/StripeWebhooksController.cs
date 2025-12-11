using Microsoft.AspNetCore.Mvc;
using Stripe;
using Zillo.Application.Services;

namespace Zillo.Dashboard.Server.Controllers;

[ApiController]
[Route("api/webhooks")]
public class StripeWebhooksController : ControllerBase
{
    private readonly IStripeConnectService _stripeConnectService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhooksController> _logger;

    public StripeWebhooksController(
        IStripeConnectService stripeConnectService,
        IConfiguration configuration,
        ILogger<StripeWebhooksController> logger)
    {
        _stripeConnectService = stripeConnectService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Handle Stripe Connect webhook events
    /// </summary>
    [HttpPost("stripe-connect")]
    public async Task<IActionResult> HandleStripeConnectWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var webhookSecret = _configuration["Stripe:ConnectWebhookSecret"];

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret
            );

            _logger.LogInformation("Received Stripe webhook: {EventType}, ID: {EventId}",
                stripeEvent.Type, stripeEvent.Id);

            switch (stripeEvent.Type)
            {
                case "account.updated":
                    await HandleAccountUpdated(stripeEvent);
                    break;

                default:
                    _logger.LogInformation("Unhandled event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook signature verification failed");
            return BadRequest(new { error = "Webhook signature verification failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook");
            return StatusCode(500, new { error = "Webhook processing failed" });
        }
    }

    private async Task HandleAccountUpdated(Event stripeEvent)
    {
        var account = stripeEvent.Data.Object as Account;
        if (account == null)
        {
            _logger.LogWarning("Could not deserialize account from webhook event");
            return;
        }

        _logger.LogInformation(
            "Processing account.updated for {AccountId}: charges_enabled={ChargesEnabled}, payouts_enabled={PayoutsEnabled}",
            account.Id, account.ChargesEnabled, account.PayoutsEnabled);

        await _stripeConnectService.UpdateAccountFromWebhookAsync(
            account.Id,
            account.ChargesEnabled,
            account.PayoutsEnabled
        );
    }
}
