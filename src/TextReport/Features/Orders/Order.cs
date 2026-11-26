using TextReport.Features.Payments;

namespace TextReport.Features.Orders;

internal sealed class Order
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public required string InputText { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public required Payment Payment { get; init; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ResultJson { get; set; }
    public DateTimeOffset? EmailSentAt { get; set; }
    public StageAttempts ProcessingAttempts { get; private set; } = new();
    public StageAttempts EmailAttempts { get; private set; } = new();
}
