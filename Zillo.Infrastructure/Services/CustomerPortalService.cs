using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using System.Security.Cryptography;

namespace Zillo.Infrastructure.Services;

public class CustomerPortalService : ICustomerPortalService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICardRepository _cardRepository;
    private readonly IUnclaimedTransactionRepository _unclaimedTransactionRepository;
    private readonly ITransactionRepository _transactionRepository;

    public CustomerPortalService(
        ICustomerRepository customerRepository,
        IAccountRepository accountRepository,
        ICardRepository cardRepository,
        IUnclaimedTransactionRepository unclaimedTransactionRepository,
        ITransactionRepository transactionRepository)
    {
        _customerRepository = customerRepository;
        _accountRepository = accountRepository;
        _cardRepository = cardRepository;
        _unclaimedTransactionRepository = unclaimedTransactionRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<CustomerDto> RegisterCustomerAsync(Guid accountId, string cardFingerprint, string name, string? email, string? phone)
    {
        // 1. Check if customer already exists with this email
        Customer? existingCustomer = null;
        if (!string.IsNullOrEmpty(email))
        {
            existingCustomer = await _customerRepository.GetByEmailAndAccountIdAsync(email, accountId);
        }

        Customer customer;
        bool isNewCustomer = false;

        if (existingCustomer != null)
        {
            // Use existing customer
            customer = existingCustomer;
        }
        else
        {
            // Create new customer
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Name = name,
                Email = email ?? string.Empty,
                PhoneNumber = phone,
                PointsBalance = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            customer = await _customerRepository.CreateAsync(customer);
            isNewCustomer = true;
        }

        // 2. Check if card is already linked to this customer
        var existingCard = await _cardRepository.GetByFingerprintAsync(cardFingerprint);

        if (existingCard == null)
        {
            // Get card details from most recent unclaimed transaction
            var unclaimedTxsForCard = await _unclaimedTransactionRepository
                .GetByCardFingerprintAsync(cardFingerprint, accountId);
            var mostRecentUnclaimed = unclaimedTxsForCard.OrderByDescending(t => t.CreatedAt).FirstOrDefault();

            // Link the card to the customer with details from unclaimed transaction
            var card = new Card
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                CardFingerprint = cardFingerprint,
                CardLast4 = mostRecentUnclaimed?.CardLast4,
                CardBrand = mostRecentUnclaimed?.CardBrand,
                CardExpMonth = mostRecentUnclaimed?.CardExpMonth,
                CardExpYear = mostRecentUnclaimed?.CardExpYear,
                IsPrimary = true,
                FirstUsedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _cardRepository.CreateAsync(card);
        }
        else if (existingCard.CustomerId != customer.Id)
        {
            // Card is linked to a different customer - this shouldn't happen in normal flow
            throw new InvalidOperationException("Card is already registered to a different customer");
        }

        // 3. Get all unclaimed transactions for this fingerprint
        var unclaimedTransactions = await _unclaimedTransactionRepository
            .GetByCardFingerprintAsync(cardFingerprint, accountId);

        int totalUnclaimedPoints = 0;

        // 4. Claim each unclaimed transaction
        foreach (var unclaimedTx in unclaimedTransactions)
        {
            // Mark as claimed
            unclaimedTx.ClaimedByCustomerId = customer.Id;
            unclaimedTx.ClaimedAt = DateTime.UtcNow;
            await _unclaimedTransactionRepository.UpdateAsync(unclaimedTx);

            // Create a transaction for the customer
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                AccountId = accountId,
                Points = unclaimedTx.Points,
                Amount = unclaimedTx.Amount,
                Type = "earn",
                Description = $"Claimed: {unclaimedTx.Description}",
                PaymentId = unclaimedTx.PaymentId,
                StripePaymentIntentId = unclaimedTx.StripePaymentIntentId,
                CreatedAt = DateTime.UtcNow
            };

            await _transactionRepository.CreateAsync(transaction);
            totalUnclaimedPoints += unclaimedTx.Points;
        }

        // 5. Get account signup bonus (only for new customers)
        int signupBonusPoints = 0;
        decimal signupBonusCash = 0;
        if (isNewCustomer)
        {
            var account = await _accountRepository.GetByIdAsync(accountId);

            if (account?.LoyaltySystemType == "points")
            {
                signupBonusPoints = account.SignupBonusPoints;

                if (signupBonusPoints > 0)
                {
                    var bonusTransaction = new Transaction
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = customer.Id,
                        AccountId = accountId,
                        Points = signupBonusPoints,
                        CashbackAmount = 0,
                        Amount = null,
                        Type = "bonus",
                        Description = "Welcome bonus",
                        CreatedAt = DateTime.UtcNow
                    };

                    await _transactionRepository.CreateAsync(bonusTransaction);
                }
            }
            else // cashback system
            {
                signupBonusCash = account?.SignupBonusCash ?? 0;

                if (signupBonusCash > 0)
                {
                    // Convert dollars to cents for storage
                    long signupBonusCents = (long)(signupBonusCash * 100);

                    var bonusTransaction = new Transaction
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = customer.Id,
                        AccountId = accountId,
                        Points = 0,
                        CashbackAmount = signupBonusCents,
                        Amount = null,
                        Type = "bonus",
                        Description = "Welcome bonus",
                        CreatedAt = DateTime.UtcNow
                    };

                    await _transactionRepository.CreateAsync(bonusTransaction);
                }
            }
        }

        // 6. Update customer balances
        customer.PointsBalance += totalUnclaimedPoints + signupBonusPoints;
        if (signupBonusCash > 0)
        {
            customer.CashbackBalance += (long)(signupBonusCash * 100); // Convert to cents
        }
        customer.UpdatedAt = DateTime.UtcNow;
        await _customerRepository.UpdateAsync(customer);

        // Calculate total spent from transactions
        var allTransactions = await _transactionRepository.GetByCustomerIdAsync(customer.Id);
        var totalSpent = allTransactions
            .Where(t => t.Type == "earn" && t.Amount.HasValue)
            .Sum(t => t.Amount ?? 0) / 100m; // Convert cents to dollars

        return new CustomerDto(
            customer.Id,
            customer.AccountId,
            customer.Name,
            customer.Email,
            customer.PhoneNumber,
            customer.PointsBalance,
            customer.CashbackBalance,
            totalSpent,
            customer.CreatedAt
        );
    }

    public async Task<CustomerPortalPreviewDto> GetSignupPreviewAsync(string accountSlug, string cardFingerprint)
    {
        var account = await _accountRepository.GetBySlugAsync(accountSlug);
        if (account == null)
        {
            throw new ArgumentException("Account not found", nameof(accountSlug));
        }

        var unclaimedPoints = await _unclaimedTransactionRepository
            .GetTotalUnclaimedPointsByFingerprintAsync(cardFingerprint, account.Id);

        var unclaimedCashback = await _unclaimedTransactionRepository
            .GetTotalUnclaimedCashbackByFingerprintAsync(cardFingerprint, account.Id);

        // Calculate signup bonus based on loyalty system type
        int signupBonusPoints = account.SignupBonusPoints;
        long signupBonusCashback = (long)(account.SignupBonusCash * 100); // Convert dollars to cents

        var totalPoints = unclaimedPoints + signupBonusPoints;
        var totalCashback = unclaimedCashback + signupBonusCashback;

        var branding = new BrandingDto(
            account.BrandingLogoUrl,
            account.BrandingPrimaryColor ?? "#DC2626",
            account.BrandingBackgroundColor ?? "#DC2626",
            account.BrandingTextColor ?? "#FFFFFF",
            account.BrandingButtonColor ?? "#E5E7EB",
            account.BrandingButtonTextColor ?? "#1F2937"
        );

        return new CustomerPortalPreviewDto(
            account.CompanyName,
            account.LoyaltySystemType ?? "points",
            signupBonusPoints,
            signupBonusCashback,
            unclaimedPoints,
            unclaimedCashback,
            totalPoints,
            totalCashback,
            branding
        );
    }

    public async Task<string> GeneratePortalTokenAsync(Guid customerId, Guid accountId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer == null || customer.AccountId != accountId)
        {
            throw new ArgumentException("Customer not found or does not belong to account");
        }

        // Generate a secure random token
        var token = GenerateSecureToken();
        var expiresAt = DateTime.UtcNow.AddDays(30); // Token valid for 30 days

        customer.PortalToken = token;
        customer.PortalTokenExpiresAt = expiresAt;
        customer.UpdatedAt = DateTime.UtcNow;

        await _customerRepository.UpdateAsync(customer);

        return token;
    }

    public async Task<CustomerPortalDto> GetPortalDataAsync(string token)
    {
        var customer = await _customerRepository.GetByPortalTokenAsync(token);

        if (customer == null)
        {
            throw new UnauthorizedAccessException("Invalid portal token");
        }

        if (customer.PortalTokenExpiresAt == null || customer.PortalTokenExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Portal token has expired");
        }

        // Get account loyalty system type
        var account = await _accountRepository.GetByIdAsync(customer.AccountId);
        if (account == null)
        {
            throw new Exception("Account not found");
        }

        // Get customer's registered cards
        var cards = await _cardRepository.GetByCustomerIdAsync(customer.Id);
        var cardInfos = cards.Select(c => new CardInfoDto(
            c.Id,
            c.CardLast4 ?? "****",
            c.CardBrand ?? "Unknown",
            c.IsPrimary,
            c.FirstUsedAt,
            c.LastUsedAt
        )).ToList();

        // Get recent transactions (last 50)
        var transactions = await _transactionRepository.GetByCustomerIdAsync(customer.Id);
        var recentTransactions = transactions
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new TransactionDto(
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
            ))
            .ToList();

        var branding = new BrandingDto(
            account.BrandingLogoUrl,
            account.BrandingPrimaryColor ?? "#DC2626",
            account.BrandingBackgroundColor ?? "#DC2626",
            account.BrandingTextColor ?? "#FFFFFF",
            account.BrandingButtonColor ?? "#E5E7EB",
            account.BrandingButtonTextColor ?? "#1F2937"
        );

        return new CustomerPortalDto(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.PhoneNumber,
            customer.PointsBalance,
            customer.CashbackBalance,
            account.LoyaltySystemType ?? "points",
            account.CompanyName,
            branding,
            cardInfos,
            recentTransactions,
            account.CashbackRate,
            account.PointsRate
        );
    }

    public async Task<bool> ValidatePortalTokenAsync(string token)
    {
        var customer = await _customerRepository.GetByPortalTokenAsync(token);

        if (customer == null)
        {
            return false;
        }

        if (customer.PortalTokenExpiresAt == null || customer.PortalTokenExpiresAt < DateTime.UtcNow)
        {
            return false;
        }

        return true;
    }

    private static string GenerateSecureToken()
    {
        // Generate a 32-byte random token and convert to base64url
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
