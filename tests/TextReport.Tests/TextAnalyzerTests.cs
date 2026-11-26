using TextReport.Product.TextAnalysis;

namespace TextReport.Tests;

public sealed class TextAnalyzerTests
{
    private readonly TextAnalyzer analyzer = new();

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void Analyze_EmptyOrWhitespaceText_ReturnsNoWordsOrSentences(string text)
    {
        var report = analyzer.Analyze(text);

        Assert.Equal(text.Length, report.CharacterCount);
        Assert.Equal(0, report.CharacterCountWithoutWhitespace);
        Assert.Equal(0, report.WordCount);
        Assert.Equal(0, report.UniqueWordCount);
        Assert.Equal(0, report.SentenceCount);
        Assert.Equal(0, report.ParagraphCount);
        Assert.Equal(0, report.AverageSentenceLengthInWords);
        Assert.Equal(0, report.LexicalDiversityPercent);
        Assert.Equal(0, report.ReadingTimeSeconds);
        Assert.Empty(report.FrequentWords);
        Assert.Empty(report.LongSentences);
        Assert.Empty(report.RepeatedSentences);
    }

    [Fact]
    public void Analyze_OneWordWithoutPunctuation_CountsOneSentence()
    {
        var report = analyzer.Analyze("Слово");

        Assert.Equal(5, report.CharacterCount);
        Assert.Equal(5, report.CharacterCountWithoutWhitespace);
        Assert.Equal(1, report.WordCount);
        Assert.Equal(1, report.UniqueWordCount);
        Assert.Equal(1, report.SentenceCount);
        Assert.Equal(1, report.ParagraphCount);
        Assert.Equal(1, report.AverageSentenceLengthInWords);
        Assert.Equal(100, report.LexicalDiversityPercent);
        Assert.Equal(new WordFrequency("слово", 1), Assert.Single(report.FrequentWords));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Analyze_OneLineBreakContinuesParagraph_BlankLineStartsAnother(string newline)
    {
        var text = $"Первая строка{newline}ещё здесь!{newline}  {newline}Второй абзац";

        var report = analyzer.Analyze(text);

        Assert.Equal(text.Length, report.CharacterCount);
        Assert.Equal(32, report.CharacterCountWithoutWhitespace);
        Assert.Equal(6, report.WordCount);
        Assert.Equal(2, report.ParagraphCount);
        Assert.Equal(2, report.SentenceCount);
        Assert.Equal(3, report.AverageSentenceLengthInWords);
    }

    [Fact]
    public void Analyze_MixedCaseAndStopWords_OnlyExcludesStopWordsFromFrequencyRanking()
    {
        var report = analyzer.Analyze("Текст, ТЕКСТ и текст. И отчёт.");

        Assert.Equal(6, report.WordCount);
        Assert.Equal(3, report.UniqueWordCount);
        Assert.Equal(50, report.LexicalDiversityPercent);
        Assert.Collection(report.FrequentWords,
            first => Assert.Equal(new WordFrequency("текст", 3), first),
            second => Assert.Equal(new WordFrequency("отчёт", 1), second));
    }

    [Fact]
    public void Analyze_FrequentWords_LimitsResultsToTenAndBreaksTiesByWord()
    {
        var report = analyzer.Analyze("l k j i h g f e d c b a");

        Assert.Equal(new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" },
            report.FrequentWords.Select(entry => entry.Word));
    }

    [Fact]
    public void Analyze_RepeatedSentences_IgnoresCaseAndExtraWhitespace()
    {
        var report = analyzer.Analyze("Тот же текст. Другой текст! ТОТ  ЖЕ ТЕКСТ.");

        Assert.Equal(3, report.SentenceCount);
        Assert.Equal(new RepeatedSentence("Тот же текст.", 2), Assert.Single(report.RepeatedSentences));
    }

    [Fact]
    public void Analyze_SentenceLength_ReportsOnlySentencesOverTwentyFiveWords()
    {
        var shortSentence = string.Join(" ", Enumerable.Repeat("слово", 25)) + ".";
        var longSentence = string.Join(" ", Enumerable.Repeat("слово", 26)) + ".";

        var report = analyzer.Analyze($"{shortSentence} {longSentence}");

        Assert.Equal(51, report.WordCount);
        Assert.Equal(new LongSentence(longSentence, 26), Assert.Single(report.LongSentences));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(100, 30)]
    [InlineData(200, 60)]
    [InlineData(201, 61)]
    public void Analyze_ReadingTime_UsesTwoHundredWordsPerMinuteRoundedUp(int wordCount, int seconds)
    {
        var text = string.Join(" ", Enumerable.Repeat("слово", wordCount));

        Assert.Equal(seconds, analyzer.Analyze(text).ReadingTimeSeconds);
    }
}
