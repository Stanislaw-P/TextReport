using TextReport.Features.Payments;

namespace TextReport.Features.Orders;

internal sealed class Order
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public required string InputText { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public required Payment Payment { get; init; }
}
