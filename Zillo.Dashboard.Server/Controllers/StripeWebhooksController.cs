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

                case "account.external_account.created":
                    await HandleExternalAccountCreated(stripeEvent);
                    break;

                case "account.external_account.updated":
                    await HandleExternalAccountUpdated(stripeEvent);
                    break;

                case "account.external_account.deleted":
                    await HandleExternalAccountDeleted(stripeEvent);
                    break;

                case "capability.updated":
                    await HandleCapabilityUpdated(stripeEvent);
                    break;

                case "account.application.deauthorized":
                    await HandleAccountDeauthorized(stripeEvent);
                    break;

                case "person.created":
                case "person.updated":
                    await HandlePersonUpdated(stripeEvent);
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

    private async Task HandleExternalAccountCreated(Event stripeEvent)
    {
        var externalAccount = stripeEvent.Data.Object as Stripe.BankAccount;
        if (externalAccount == null)
        {
            _logger.LogWarning("Could not deserialize external_account from webhook event");
            return;
        }

        _logger.LogInformation(
            "Bank account created for account {AccountId}: {BankName} ending in {Last4}",
            externalAccount.Account, externalAccount.BankName, externalAccount.Last4);

        // Optional: Store bank account info if needed
        await Task.CompletedTask;
    }

    private async Task HandleExternalAccountUpdated(Event stripeEvent)
    {
        var externalAccount = stripeEvent.Data.Object as Stripe.BankAccount;
        if (externalAccount == null)
        {
            _logger.LogWarning("Could not deserialize external_account from webhook event");
            return;
        }

        _logger.LogInformation(
            "Bank account updated for account {AccountId}: {BankName} ending in {Last4}",
            externalAccount.Account, externalAccount.BankName, externalAccount.Last4);

        await Task.CompletedTask;
    }

    private async Task HandleExternalAccountDeleted(Event stripeEvent)
    {
        var externalAccount = stripeEvent.Data.Object as Stripe.BankAccount;
        if (externalAccount == null)
        {
            _logger.LogWarning("Could not deserialize external_account from webhook event");
            return;
        }

        _logger.LogInformation(
            "Bank account deleted for account {AccountId}: {BankName} ending in {Last4}",
            externalAccount.Account, externalAccount.BankName, externalAccount.Last4);

        await Task.CompletedTask;
    }

    private async Task HandleCapabilityUpdated(Event stripeEvent)
    {
        var capability = stripeEvent.Data.Object as Capability;
        if (capability == null)
        {
            _logger.LogWarning("Could not deserialize capability from webhook event");
            return;
        }

        _logger.LogInformation(
            "Capability {CapabilityId} updated for account {AccountId}: status={Status}",
            capability.Id, capability.Account, capability.Status);

        // Capability status changes might affect charges_enabled/payouts_enabled
        // The account.updated event will handle the actual status update
        await Task.CompletedTask;
    }

    private async Task HandleAccountDeauthorized(Event stripeEvent)
    {
        var account = stripeEvent.Data.Object as Account;
        if (account == null)
        {
            _logger.LogWarning("Could not deserialize account from webhook event");
            return;
        }

        _logger.LogWarning(
            "Account {AccountId} deauthorized - disconnected from platform",
            account.Id);

        // Mark account as disconnected
        await _stripeConnectService.HandleAccountDeauthorizedAsync(account.Id);
    }

    private async Task HandlePersonUpdated(Event stripeEvent)
    {
        var person = stripeEvent.Data.Object as Person;
        if (person == null)
        {
            _logger.LogWarning("Could not deserialize person from webhook event");
            return;
        }

        _logger.LogInformation(
            "Person {PersonId} {EventType} for account {AccountId}",
            person.Id, stripeEvent.Type, person.Account);

        // Optional: Track person verification status if needed
        await Task.CompletedTask;
    }
}
