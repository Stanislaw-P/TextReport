using System.Text.Json;

namespace TextReport.Features.Orders.ProcessOrder;

internal sealed class DevelopmentOrderProcessor : IOrderProcessor
{
    public Task<string> ProcessAsync(string inputText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(JsonSerializer.Serialize(new { isTestReport = true, characterCount = inputText.Length }));
    }
}
