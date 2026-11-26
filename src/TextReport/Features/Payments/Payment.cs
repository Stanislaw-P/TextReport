using TextReport.Features.Orders;

namespace TextReport.Features.Payments;

internal sealed class Payment
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public Order Order { get; private set; } = null!;
    public decimal Amount { get; init; }
    public required string Currency { get; init; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public StageAttempts Attempts { get; private set; } = new();
}

internal enum PaymentStatus
{
    Pending,
    Paid
}
