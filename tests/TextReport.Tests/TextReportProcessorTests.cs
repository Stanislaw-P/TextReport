using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TextReport.Product.TextAnalysis;

namespace TextReport.Tests;

public sealed class TextReportProcessorTests
{
    private const string Input = "Break up the first paragraph.";

    [Fact]
    public async Task ProcessAsync_WithoutAi_ReturnsBaseReport()
    {
        using var report = await ProcessAsync();

        AssertBaseReport(report.RootElement);
        Assert.False(report.RootElement.TryGetProperty("recommendations", out _));
    }

    [Fact]
    public async Task ProcessAsync_WithAi_SendsTextAndStatisticsAndKeepsAtMostFiveSuggestions()
    {
        var chat = new FakeChat((history, _) =>
        {
            Assert.Collection(history,
                message => Assert.Equal(AuthorRole.System, message.Role),
                message =>
                {
                    Assert.Equal(AuthorRole.User, message.Role);
                    using var payload = JsonDocument.Parse(message.Content!);
                    Assert.Equal(Input, payload.RootElement.GetProperty("text").GetString());
                    AssertBaseReport(payload.RootElement.GetProperty("statistics"));
                    Assert.Equal(2, payload.RootElement.EnumerateObject().Count());
                });
            return Reply("""
                {"wordCount": 999, "recommendations": [" First ", "", null, "Second", "Third", "Fourth", "Fifth", "Sixth"]}
                """);
        });

        using var report = await ProcessAsync(chat);

        AssertBaseReport(report.RootElement);
        Assert.Equal(new[] { "First", "Second", "Third", "Fourth", "Fifth" },
            report.RootElement.GetProperty("recommendations").EnumerateArray().Select(item => item.GetString()));
    }

    [Theory]
    [InlineData("bad JSON")]
    [InlineData("""{"recommendations": []}""")]
    [InlineData("""{"recommendations": "not a list"}""")]
    public async Task ProcessAsync_UnusableAiResponse_ReturnsBaseReport(string response)
    {
        using var report = await ProcessAsync(new FakeChat((_, _) => Reply(response)));

        AssertBaseReport(report.RootElement);
        Assert.False(report.RootElement.TryGetProperty("recommendations", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessAsync_FailedOrTimedOutAi_ReturnsBaseReport(bool timeout)
    {
        var chat = new FakeChat((_, _) => Task.FromException<IReadOnlyList<ChatMessageContent>>(
            timeout ? new TaskCanceledException() : new HttpRequestException()));

        using var report = await ProcessAsync(chat);

        AssertBaseReport(report.RootElement);
        Assert.False(report.RootElement.TryGetProperty("recommendations", out _));
    }

    [Fact]
    public async Task ProcessAsync_WhenApplicationCancels_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var chat = new FakeChat((_, token) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<IReadOnlyList<ChatMessageContent>>(token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => NewProcessor(chat).ProcessAsync(Input, cancellation.Token));
    }

    private static async Task<JsonDocument> ProcessAsync(IChatCompletionService? chat = null) =>
        JsonDocument.Parse(await NewProcessor(chat).ProcessAsync(Input, TestContext.Current.CancellationToken));

    private static TextReportProcessor NewProcessor(IChatCompletionService? chat) =>
        new(new TextAnalyzer(), new AiRecommendations(NullLogger<AiRecommendations>.Instance, chat));

    private static void AssertBaseReport(JsonElement report)
    {
        Assert.Equal(Input.Length, report.GetProperty("characterCount").GetInt32());
        Assert.Equal(5, report.GetProperty("wordCount").GetInt32());
        Assert.Equal(1, report.GetProperty("sentenceCount").GetInt32());
    }

    private static Task<IReadOnlyList<ChatMessageContent>> Reply(string text) =>
        Task.FromResult<IReadOnlyList<ChatMessageContent>>([new ChatMessageContent(AuthorRole.Assistant, text)]);

    private sealed class FakeChat(
        Func<ChatHistory, CancellationToken, Task<IReadOnlyList<ChatMessageContent>>> respond) : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null, CancellationToken cancellationToken = default) =>
            respond(chatHistory, cancellationToken);

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
