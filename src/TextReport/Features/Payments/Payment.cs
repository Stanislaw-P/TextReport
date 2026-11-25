namespace TextReport.Features.Payments;

internal sealed class Payment
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }
    public required string Currency { get; init; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; }
}

internal enum PaymentStatus
{
    Pending
}
