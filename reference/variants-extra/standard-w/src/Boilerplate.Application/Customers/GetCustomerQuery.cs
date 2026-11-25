using System.Text.Json.Serialization;
using FluentValidation;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Application.Customers;

public record GetCustomerQuery : IQuery<CustomerDto>
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public Guid Id { get; set; }
}

public class GetCustomerQueryHandler(ICustomerReader customerReader, IEnumerable<IValidator<GetCustomerQuery>> validators) : BaseQueryHandler<GetCustomerQuery, CustomerDto>(validators)
{
    protected override async Task<CustomerDto> OnHandle(GetCustomerQuery query)
    {
        var customerView = await customerReader.FindAsync(query.Id) ?? throw new NotFoundException(nameof(Customer));

        return CustomerDto.From(customerView);
    }
}
