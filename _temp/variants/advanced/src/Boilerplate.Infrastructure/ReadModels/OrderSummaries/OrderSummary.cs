using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.ReadModels.OrderSummaries;

public class OrderSummary : IMultiTenant
{
    public Guid OrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public string OrderNumber { get; private set; }
    public string CustomerName { get; private set; }
    public string Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public int LineCount { get; private set; }
}
