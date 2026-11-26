using System.Text.Json;
using TextReport.Features.Notifications.SendEmail;
using TextReport.Product.TextAnalysis;

namespace TextReport.Tests;

public sealed class EmailReportRendererTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Render_IncludesReportSectionsAndEncodesDynamicHtml()
    {
        var orderId = Guid.NewGuid();
        var report = new TextAnalyzer().Analyze("Текст и текст. Новое предложение.") with
        {
            FrequentWords = [new WordFrequency("<b>слово</b>", 2)],
            LongSentences = [new LongSentence("<script>длинное</script>", 26)],
            RepeatedSentences = [new RepeatedSentence("<i>повтор</i>", 2)],
            Recommendations = ["<img src=x onerror=alert(1)>"]
        };

        var html = EmailReportRenderer.Render(orderId, JsonSerializer.Serialize(report, JsonOptions));

        Assert.Contains(orderId.ToString(), html);
        Assert.Contains("Статистика текста", html);
        Assert.Contains("<table", html);
        Assert.Contains("Замечания", html);
        Assert.Contains("AI-рекомендации", html);
        Assert.Contains("&lt;b&gt;слово&lt;/b&gt;", html);
        Assert.Contains("&lt;script&gt;длинное&lt;/script&gt;", html);
        Assert.Contains("&lt;i&gt;повтор&lt;/i&gt;", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
        Assert.DoesNotContain("<b>слово", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<i>повтор", html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public void Render_WithoutRecommendations_IncludesBaseReport()
    {
        var report = new TextAnalyzer().Analyze("и в на.");

        var html = EmailReportRenderer.Render(Guid.NewGuid(), JsonSerializer.Serialize(report, JsonOptions));

        Assert.Contains("Статистика текста", html);
        Assert.Contains("Нет данных", html);
        Assert.Contains("Длинных и повторяющихся предложений нет.", html);
        Assert.DoesNotContain("AI-рекомендации", html);
    }

    [Fact]
    public void Render_LegacyTestReport_ShowsOnlyAvailableStatistic()
    {
        var html = EmailReportRenderer.Render(Guid.NewGuid(), """{"isTestReport":true,"characterCount":42}""");

        Assert.Contains("Пробный отчёт", html);
        Assert.Contains("Символы: 42", html);
        Assert.DoesNotContain("Частые слова", html);
    }
}
