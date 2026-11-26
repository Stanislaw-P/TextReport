using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using TextReport.Product.TextAnalysis;

namespace TextReport.Features.Notifications.SendEmail;

internal static class EmailReportRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    public static string Render(Guid orderId, string resultJson)
    {
        using var document = JsonDocument.Parse(resultJson);
        var root = document.RootElement;
        var html = new StringBuilder($"""
            <!doctype html>
            <html lang="ru">
            <body style="font-family: Arial, sans-serif; color: #182438;">
            <h1>TextReport</h1>
            <p>Номер заявки: <strong>{orderId}</strong></p>
            """);

        if (root.TryGetProperty("isTestReport", out var testReport) && testReport.ValueKind == JsonValueKind.True)
        {
            html.Append($"<p>Пробный отчёт. Символы: {root.GetProperty("characterCount").GetInt32()}.</p>");
        }
        else
        {
            var report = root.Deserialize<TextAnalysisResult>(JsonOptions)
                ?? throw new JsonException("Analysis report is missing.");
            AppendAnalysis(html, report);
        }

        return html.Append("</body></html>").ToString();
    }

    private static void AppendAnalysis(StringBuilder html, TextAnalysisResult report)
    {
        html.Append($"""
            <h2>Статистика текста</h2>
            <ul>
              <li>Символы: {report.CharacterCount} (без пробельных знаков: {report.CharacterCountWithoutWhitespace})</li>
              <li>Слова: {report.WordCount}, из них уникальные: {report.UniqueWordCount}</li>
              <li>Предложения: {report.SentenceCount}, абзацы: {report.ParagraphCount}</li>
              <li>Средняя длина предложения: {report.AverageSentenceLengthInWords.ToString("0.##", Russian)} слов</li>
              <li>Лексическое разнообразие: {report.LexicalDiversityPercent.ToString("0.##", Russian)}%</li>
              <li>Время чтения: {report.ReadingTimeSeconds / 60} мин {report.ReadingTimeSeconds % 60} сек</li>
            </ul>
            <h2>Частые слова</h2>
            <table style="border-collapse: collapse;">
              <thead><tr><th scope="col">Слово</th><th scope="col">Количество</th></tr></thead>
              <tbody>
            """);
        foreach (var word in report.FrequentWords)
            html.Append($"<tr><td>{WebUtility.HtmlEncode(word.Word)}</td><td>{word.Count}</td></tr>");
        if (report.FrequentWords.Count == 0)
            html.Append("<tr><td colspan=\"2\">Нет данных</td></tr>");
        html.Append("</tbody></table><h2>Замечания</h2>");

        if (report.LongSentences.Count == 0 && report.RepeatedSentences.Count == 0)
            html.Append("<p>Длинных и повторяющихся предложений нет.</p>");
        if (report.LongSentences.Count > 0)
            AppendList(html, "Длинные предложения", report.LongSentences.Select(x => $"{x.Text} ({x.WordCount} слов)"));
        if (report.RepeatedSentences.Count > 0)
            AppendList(html, "Повторяющиеся предложения", report.RepeatedSentences.Select(x => $"{x.Text} ({x.Count})"));
        if (report.Recommendations is { Count: > 0 } recommendations)
            AppendList(html, "AI-рекомендации", recommendations);
    }

    private static void AppendList(StringBuilder html, string title, IEnumerable<string> items)
    {
        html.Append($"<h3>{WebUtility.HtmlEncode(title)}</h3><ul>");
        foreach (var item in items)
            html.Append($"<li>{WebUtility.HtmlEncode(item)}</li>");
        html.Append("</ul>");
    }
}
