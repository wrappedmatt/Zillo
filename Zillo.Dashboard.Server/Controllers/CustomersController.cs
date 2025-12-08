using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Zillo.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Zillo.Dashboard.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly IAuthService _authService;
    private readonly ICustomerPortalService _portalService;
    private readonly IWalletService _walletService;
    private readonly ICardRepository _cardRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        ICustomerService customerService,
        IAuthService authService,
        ICustomerPortalService portalService,
        IWalletService walletService,
        ICardRepository cardRepository,
        ITransactionRepository transactionRepository,
        ILogger<CustomersController> logger)
    {
        _customerService = customerService;
        _authService = authService;
        _portalService = portalService;
        _walletService = walletService;
        _cardRepository = cardRepository;
        _transactionRepository = transactionRepository;
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
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers()
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var customers = await _customerService.GetCustomersByAccountIdAsync(accountId.Value);
            return Ok(customers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customers");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var customer = await _customerService.GetCustomerByIdAsync(id, accountId.Value);
            if (customer == null)
                return NotFound();

            return Ok(customer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var customer = await _customerService.CreateCustomerAsync(request, accountId.Value);
            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CustomerDto>> UpdateCustomer(Guid id, [FromBody] UpdateCustomerRequest request)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            var customer = await _customerService.UpdateCustomerAsync(id, request, accountId.Value);
            return Ok(customer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(Guid id)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            await _customerService.DeleteCustomerAsync(id, accountId.Value);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting customer");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}/cards")]
    public async Task<IActionResult> GetCustomerCards(Guid id)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            // Verify customer belongs to this account
            var customer = await _customerService.GetCustomerByIdAsync(id, accountId.Value);
            if (customer == null)
                return NotFound();

            var cards = await _cardRepository.GetByCustomerIdAsync(id);
            var cardDtos = cards.Select(c => new CardInfoDto(
                c.Id,
                c.CardLast4 ?? "",
                c.CardBrand ?? "",
                c.IsPrimary,
                c.FirstUsedAt,
                c.LastUsedAt
            ));
            return Ok(cardDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer cards");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}/transactions")]
    public async Task<IActionResult> GetCustomerTransactions(Guid id)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            // Verify customer belongs to this account
            var customer = await _customerService.GetCustomerByIdAsync(id, accountId.Value);
            if (customer == null)
                return NotFound();

            var transactions = await _transactionRepository.GetByCustomerIdAsync(id);
            var transactionDtos = transactions.Select(t => new TransactionDto(
                t.Id,
                t.CustomerId,
                t.Points,
                t.CashbackAmount,
                t.Amount,
                t.Type,
                t.Description,
                t.CreatedAt,
                t.PaymentId,
                t.StripePaymentIntentId
            ));
            return Ok(transactionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer transactions");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/portal-token")]
    public async Task<IActionResult> GeneratePortalToken(Guid id)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            // Verify customer belongs to this account
            var customer = await _customerService.GetCustomerByIdAsync(id, accountId.Value);
            if (customer == null)
                return NotFound();

            var token = await _portalService.GeneratePortalTokenAsync(id, accountId.Value);

            return Ok(new { token, portalUrl = $"/portal/{token}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating portal token");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/adjustment")]
    public async Task<IActionResult> CreateAdjustment(Guid id, [FromBody] CreateAdjustmentRequest request)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            // Verify customer belongs to this account
            var customer = await _customerService.GetCustomerByIdAsync(id, accountId.Value);
            if (customer == null)
                return NotFound();

            // Create adjustment transaction
            await _customerService.CreateAdjustmentAsync(id, accountId.Value, request);

            return Ok(new { message = "Adjustment created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating adjustment");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/send-notification")]
    public async Task<IActionResult> SendNotification(Guid id, [FromBody] SendNotificationRequest request)
    {
        try
        {
            var accountId = await GetAccountIdFromToken();
            if (accountId == null)
                return Unauthorized();

            // Verify customer belongs to this account
            var customer = await _customerService.GetCustomerByIdAsync(id, accountId.Value);
            if (customer == null)
                return NotFound();

            // Send push notification with custom message
            await _walletService.SendCustomMessageNotificationAsync(id, request.Message);

            return Ok(new { message = "Notification sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification");
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record SendNotificationRequest(string Message);
