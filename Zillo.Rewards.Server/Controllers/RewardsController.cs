using Zillo.Application.Services;
using Zillo.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Zillo.Rewards.Server.Controllers;

[ApiController]
[Route("api/{slug}/[controller]")]
public class RewardsController : ControllerBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly ICustomerService _customerService;
    private readonly ITransactionService _transactionService;

    public RewardsController(
        IAccountRepository accountRepository,
        ICustomerService customerService,
        ITransactionService transactionService)
    {
        _accountRepository = accountRepository;
        _customerService = customerService;
        _transactionService = transactionService;
    }

    [HttpGet("account")]
    public async Task<IActionResult> GetAccount(string slug)
    {
        var account = await _accountRepository.GetBySlugAsync(slug);

        if (account == null)
            return NotFound(new { message = "Rewards program not found" });

        return Ok(new
        {
            companyName = account.CompanyName,
            slug = account.Slug,
            loyaltySystemType = account.LoyaltySystemType,
            cashbackRate = account.CashbackRate,
            branding = new
            {
                logoUrl = account.BrandingLogoUrl,
                primaryColor = account.BrandingPrimaryColor,
                backgroundColor = account.BrandingBackgroundColor,
                textColor = account.BrandingTextColor,
                buttonColor = account.BrandingButtonColor,
                buttonTextColor = account.BrandingButtonTextColor
            }
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> CustomerLogin(string slug, [FromBody] CustomerLoginRequest request)
    {
        try
        {
            var account = await _accountRepository.GetBySlugAsync(slug);

            if (account == null)
                return NotFound(new { message = "Rewards program not found" });

            // Find customer by email
            var customer = await _customerService.GetCustomerByEmailAsync(request.Email, account.Id);

            if (customer == null)
                return Unauthorized(new { message = "Customer not found. Please check your email address." });

            // Get customer's transactions
            var transactions = await _transactionService.GetTransactionsByCustomerIdAsync(
                customer.Id,
                account.Id);

            return Ok(new
            {
                customer = new
                {
                    id = customer.Id,
                    name = customer.Name,
                    email = customer.Email,
                    phoneNumber = customer.PhoneNumber,
                    pointsBalance = customer.PointsBalance,
                    cashbackBalance = customer.CashbackBalance
                },
                transactions = transactions.Select(t => new
                {
                    id = t.Id,
                    points = t.Points,
                    cashbackAmount = t.CashbackAmount,
                    type = t.Type,
                    description = t.Description,
                    amount = t.Amount,
                    createdAt = t.CreatedAt
                })
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public record CustomerLoginRequest(string Email);
