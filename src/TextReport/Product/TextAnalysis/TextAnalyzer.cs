using System.Text.RegularExpressions;

namespace TextReport.Product.TextAnalysis;

internal sealed class TextAnalyzer
{
    private const int ReadingSpeedWordsPerMinute = 200;
    private const int LongSentenceThreshold = 25;

    private static readonly Regex ParagraphBreak = new(@"\n[^\S\n]*\n");
    private static readonly Regex SentencePattern = new(@"[^.!?…]+[.!?…]*(?:[»”’"")\]]+)?");
    private static readonly Regex WordPattern = new(@"[\p{L}\p{Nd}][\p{L}\p{M}\p{Nd}]*(?:[-'’][\p{L}\p{Nd}][\p{L}\p{M}\p{Nd}]*)*");
    private static readonly Regex Whitespace = new(@"\s+");

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "а", "без", "бы", "в", "во", "для", "до", "да", "же", "за", "и", "из", "или",
        "к", "как", "ко", "ли", "либо", "между", "на", "над", "не", "ни", "но", "о", "об",
        "от", "перед", "по", "под", "при", "про", "с", "со", "так", "то", "у", "через",
        "что", "чтобы", "это"
    };

    public TextAnalysisResult Analyze(string text)
    {
        var paragraphs = ParagraphBreak.Split(text.ReplaceLineEndings("\n"))
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .ToArray();
        var sentences = paragraphs
            .SelectMany(paragraph => SentencePattern.Matches(paragraph))
            .Select(match => new
            {
                Text = match.Value.Trim(),
                Words = WordPattern.Matches(match.Value)
                    .Select(word => word.Value.ToLowerInvariant())
                    .ToArray()
            })
            .Where(sentence => sentence.Words.Length > 0)
            .ToArray();

        var words = sentences.SelectMany(sentence => sentence.Words).ToArray();
        var wordGroups = words.GroupBy(word => word, StringComparer.Ordinal).ToArray();
        var frequentWords = wordGroups
            .Where(group => !StopWords.Contains(group.Key))
            .Select(group => new WordFrequency(group.Key, group.Count()))
            .OrderByDescending(word => word.Count)
            .ThenBy(word => word.Word, StringComparer.Ordinal)
            .Take(10)
            .ToArray();

        var longSentences = sentences
            .Where(sentence => sentence.Words.Length > LongSentenceThreshold)
            .Select(sentence => new LongSentence(sentence.Text, sentence.Words.Length))
            .ToArray();
        var repeatedSentences = sentences
            .GroupBy(sentence => Whitespace.Replace(sentence.Text, " "), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new RepeatedSentence(group.First().Text, group.Count()))
            .OrderByDescending(sentence => sentence.Count)
            .ToArray();

        return new TextAnalysisResult(
            CharacterCount: text.Length,
            CharacterCountWithoutWhitespace: text.Count(character => !char.IsWhiteSpace(character)),
            WordCount: words.Length,
            SentenceCount: sentences.Length,
            ParagraphCount: paragraphs.Length,
            UniqueWordCount: wordGroups.Length,
            AverageSentenceLengthInWords: sentences.Length == 0 ? 0 : (double)words.Length / sentences.Length,
            LexicalDiversityPercent: words.Length == 0 ? 0 : 100d * wordGroups.Length / words.Length,
            ReadingTimeSeconds: (int)Math.Ceiling(words.Length * 60d / ReadingSpeedWordsPerMinute),
            FrequentWords: frequentWords,
            LongSentences: longSentences,
            RepeatedSentences: repeatedSentences);
    }
}
