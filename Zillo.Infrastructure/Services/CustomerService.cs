using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;

namespace Zillo.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ITransactionRepository _transactionRepository;

    public CustomerService(ICustomerRepository customerRepository, ITransactionRepository transactionRepository)
    {
        _customerRepository = customerRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<IEnumerable<CustomerDto>> GetCustomersByAccountIdAsync(Guid accountId)
    {
        var customers = await _customerRepository.GetByAccountIdAsync(accountId);
        var customerDtos = new List<CustomerDto>();

        foreach (var customer in customers)
        {
            customerDtos.Add(await MapToDtoAsync(customer));
        }

        return customerDtos;
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(Guid id, Guid accountId)
    {
        var customer = await _customerRepository.GetByIdAsync(id);

        if (customer == null || customer.AccountId != accountId)
            return null;

        return await MapToDtoAsync(customer);
    }

    public async Task<CustomerDto?> GetCustomerByEmailAsync(string email, Guid accountId)
    {
        var customers = await _customerRepository.GetByAccountIdAsync(accountId);
        var customer = customers.FirstOrDefault(c =>
            c.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

        return customer != null ? await MapToDtoAsync(customer) : null;
    }

    public async Task<CustomerDto?> FindCustomerByEmailAsync(string email)
    {
        var customer = await _customerRepository.GetByEmailAsync(email);
        return customer != null ? await MapToDtoAsync(customer) : null;
    }

    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request, Guid accountId)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Name = request.Name,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            PointsBalance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _customerRepository.CreateAsync(customer);
        return await MapToDtoAsync(created);
    }

    public async Task<CustomerDto> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request, Guid accountId)
    {
        var customer = await _customerRepository.GetByIdAsync(id);

        if (customer == null || customer.AccountId != accountId)
            throw new Exception("Customer not found");

        customer.Name = request.Name;
        customer.Email = request.Email;
        customer.PhoneNumber = request.PhoneNumber;
        customer.UpdatedAt = DateTime.UtcNow;

        var updated = await _customerRepository.UpdateAsync(customer);
        return await MapToDtoAsync(updated);
    }

    public async Task DeleteCustomerAsync(Guid id, Guid accountId)
    {
        var customer = await _customerRepository.GetByIdAsync(id);

        if (customer == null || customer.AccountId != accountId)
            throw new Exception("Customer not found");

        await _customerRepository.DeleteAsync(id);
    }

    public async Task CreateAdjustmentAsync(Guid customerId, Guid accountId, CreateAdjustmentRequest request)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);

        if (customer == null || customer.AccountId != accountId)
            throw new Exception("Customer not found");

        // Create adjustment transaction
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            AccountId = accountId,
            Type = "adjustment",
            Description = request.Description,
            Points = request.Points ?? 0,
            CashbackAmount = request.CashbackAmount ?? 0,
            CreatedAt = DateTime.UtcNow
        };

        await _transactionRepository.CreateAsync(transaction);

        // Update customer balance
        if (request.Points.HasValue)
        {
            customer.PointsBalance += request.Points.Value;
        }
        if (request.CashbackAmount.HasValue)
        {
            customer.CashbackBalance += request.CashbackAmount.Value;
        }

        customer.UpdatedAt = DateTime.UtcNow;
        await _customerRepository.UpdateAsync(customer);
    }

    private async Task<CustomerDto> MapToDtoAsync(Customer customer)
    {
        // Calculate total spent from transactions
        var transactions = await _transactionRepository.GetByCustomerIdAsync(customer.Id);
        var totalSpent = transactions
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
}
