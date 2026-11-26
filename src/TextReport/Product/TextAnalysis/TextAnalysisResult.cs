using System.Text.Json.Serialization;

namespace TextReport.Product.TextAnalysis;

internal sealed record TextAnalysisResult(
    int CharacterCount,
    int CharacterCountWithoutWhitespace,
    int WordCount,
    int SentenceCount,
    int ParagraphCount,
    int UniqueWordCount,
    double AverageSentenceLengthInWords,
    double LexicalDiversityPercent,
    int ReadingTimeSeconds,
    IReadOnlyList<WordFrequency> FrequentWords,
    IReadOnlyList<LongSentence> LongSentences,
    IReadOnlyList<RepeatedSentence> RepeatedSentences)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Recommendations { get; init; }
}

internal sealed record WordFrequency(string Word, int Count);
internal sealed record LongSentence(string Text, int WordCount);
internal sealed record RepeatedSentence(string Text, int Count);
