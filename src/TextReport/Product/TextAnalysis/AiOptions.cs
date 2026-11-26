namespace TextReport.Product.TextAnalysis;

internal sealed class AiOptions
{
    public bool Enabled { get; init; } = true;
    public string? Endpoint { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }
}
