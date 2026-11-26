using System.Text.Json;
using TextReport.Features.Orders.ProcessOrder;

namespace TextReport.Product.TextAnalysis;

internal sealed class TextReportProcessor(TextAnalyzer analyzer, AiRecommendations ai) : IOrderProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> ProcessAsync(string inputText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = analyzer.Analyze(inputText);
        var recommendations = await ai.GetAsync(inputText, result, cancellationToken);
        return JsonSerializer.Serialize(result with { Recommendations = recommendations }, JsonOptions);
    }
}
