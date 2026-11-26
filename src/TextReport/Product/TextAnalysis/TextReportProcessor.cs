using System.Text.Json;
using TextReport.Features.Orders.ProcessOrder;

namespace TextReport.Product.TextAnalysis;

internal sealed class TextReportProcessor(TextAnalyzer analyzer) : IOrderProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<string> ProcessAsync(string inputText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = analyzer.Analyze(inputText);
        return Task.FromResult(JsonSerializer.Serialize(result, JsonOptions));
    }
}
