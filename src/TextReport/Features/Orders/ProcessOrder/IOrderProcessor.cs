namespace TextReport.Features.Orders.ProcessOrder;

internal interface IOrderProcessor
{
    Task<string> ProcessAsync(string inputText, CancellationToken cancellationToken);
}
