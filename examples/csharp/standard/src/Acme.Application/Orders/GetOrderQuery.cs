using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Orders;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;
using FluentValidation;

namespace Acme.Application.Orders;

public record GetOrderQuery : IQuery<OrderDto>
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public Guid Id { get; set; }
}

public class GetOrderQueryHandler(IOrderReader orderReader, IEnumerable<IValidator<GetOrderQuery>> validators) : BaseQueryHandler<GetOrderQuery, OrderDto>(validators)
{
    protected override async Task<OrderDto> OnHandle(GetOrderQuery query)
    {
        var orderView = await orderReader.FindAsync(query.Id) ?? throw new NotFoundException(nameof(Order));

        return OrderDto.From(orderView);
    }
}
