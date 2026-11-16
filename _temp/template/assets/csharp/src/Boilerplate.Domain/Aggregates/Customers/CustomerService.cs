using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Customers;

public interface ICustomerService
{
    Task<Customer> CreateAsync(AuditUser user, Guid id, string name, string email);
    Task<Customer> GetAsync(Guid id);
    Task<Customer> BlockAsync(AuditUser user, Guid id);
    Task<Customer> RegisterCompletedOrderAsync(AuditUser user, Guid id);
}

public class CustomerService(ICustomerRepository customerRepository) : ICustomerService
{
    public async Task<Customer> CreateAsync(AuditUser user, Guid id, string name, string email)
    {
        var customer = new Customer(id, name, email, user);

        if (await customerRepository.IsEmailExistsAsync(customer.Email))
        {
            throw new DuplicateException(nameof(Customer));
        }

        await customerRepository.CreateAsync(customer);

        return customer;
    }

    public async Task<Customer> GetAsync(Guid id)
    {
        return await customerRepository.FindByIdAsync(id)
            ?? throw new NotFoundException(nameof(Customer));
    }

    public async Task<Customer> BlockAsync(AuditUser user, Guid id)
    {
        var customer = await customerRepository.FindByIdTrackingAsync(id)
            ?? throw new NotFoundException(nameof(Customer));

        customer.Block();
        customer.SetUpdatedAudit(user);

        await customerRepository.UpdateAsync(customer);

        return customer;
    }

    public async Task<Customer> RegisterCompletedOrderAsync(AuditUser user, Guid id)
    {
        var customer = await customerRepository.FindByIdTrackingAsync(id)
            ?? throw new NotFoundException(nameof(Customer));

        customer.RegisterCompletedOrder();
        customer.SetUpdatedAudit(user);

        await customerRepository.UpdateAsync(customer);

        return customer;
    }
}
