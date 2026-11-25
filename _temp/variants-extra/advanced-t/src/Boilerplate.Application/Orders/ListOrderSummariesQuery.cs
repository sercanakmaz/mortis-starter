using System.Text.Json.Serialization;
using FluentValidation;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Orders;

public record ListOrderSummariesQuery : IQuery<PagedResult<OrderSummaryView>>
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public int Page { get; set; } = 1;
    public int Size { get; set; } = PageRequest.DefaultSize;
    public string Status { get; set; }
}

public class ListOrderSummariesQueryValidator : AbstractValidator<ListOrderSummariesQuery>
{
    public ListOrderSummariesQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.Size).InclusiveBetween(1, PageRequest.MaxSize);
        RuleFor(query => query.Status).IsEnumName(typeof(OrderStatus)).When(query => query.Status is not null);
    }
}

public class ListOrderSummariesQueryHandler(IOrderSummaryReader orderSummaryReader, IEnumerable<IValidator<ListOrderSummariesQuery>> validators) : BaseQueryHandler<ListOrderSummariesQuery, PagedResult<OrderSummaryView>>(validators)
{
    protected override Task<PagedResult<OrderSummaryView>> OnHandle(ListOrderSummariesQuery query)
    {
        return orderSummaryReader.ListAsync(query.Page, query.Size, query.Status);
    }
}
