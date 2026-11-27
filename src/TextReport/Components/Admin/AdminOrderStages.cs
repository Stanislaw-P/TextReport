using TextReport.Features;
using TextReport.Features.Orders;

namespace TextReport.Components.Admin;

internal sealed record AdminOrderStage(
    string Name, DateTimeOffset? CompletedAt, bool Ready, StageAttempts Attempts)
{
    public string Status(int maxAttempts) =>
        CompletedAt is not null ? "Завершено" :
        !Ready ? "Ожидает предыдущий этап" :
        Attempts.LastError is null ? "В очереди" :
        Attempts.Count >= maxAttempts ? "Попытки исчерпаны" : "Ожидает повтора";

    public static AdminOrderStage[] For(Order order) =>
    [
        new("Оплата", order.PaidAt, true, order.Payment.Attempts),
        new("Обработка", order.ProcessedAt, order.PaidAt is not null, order.ProcessingAttempts),
        new("Письмо", order.EmailSentAt, order.ProcessedAt is not null && order.ResultJson is not null,
            order.EmailAttempts)
    ];
}
