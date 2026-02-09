using DaisiBot.Agent.Processing;

namespace DaisiBot.Agent.Tests.Processing;

public class ContentCleanerTests
{
    [Fact]
    public void Clean_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ContentCleaner.Clean(null!));
    }

    [Fact]
    public void Clean_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ContentCleaner.Clean(""));
    }

    [Fact]
    public void Clean_PlainText_ReturnsTrimmed()
    {
        Assert.Equal("Hello world", ContentCleaner.Clean("Hello world"));
    }

    [Fact]
    public void Clean_RemovesThinkTags()
    {
        var input = "<think>internal reasoning here</think>The actual answer";
        var result = ContentCleaner.Clean(input);
        Assert.Equal("The actual answer", result);
    }

    [Fact]
    public void Clean_RemovesMultilineThinkTags()
    {
        var input = """
            <think>
            Let me think about this...
            I should consider multiple factors.
            </think>
            Here is my answer.
            """;
        var result = ContentCleaner.Clean(input);
        Assert.Contains("Here is my answer.", result);
        Assert.DoesNotContain("<think>", result);
        Assert.DoesNotContain("</think>", result);
        Assert.DoesNotContain("Let me think", result);
    }

    [Fact]
    public void Clean_RemovesResponseTags()
    {
        var input = "<response>The answer is 42</response>";
        var result = ContentCleaner.Clean(input);
        Assert.Equal("The answer is 42", result);
    }

    [Fact]
    public void Clean_RemovesBothThinkAndResponseTags()
    {
        var input = "<think>thinking...</think><response>The answer</response>";
        var result = ContentCleaner.Clean(input);
        Assert.Equal("The answer", result);
    }

    [Fact]
    public void Clean_TrimsAntiPrompt_User()
    {
        var input = "Here is my answer User:";
        var result = ContentCleaner.Clean(input);
        Assert.Equal("Here is my answer", result);
    }

    [Fact]
    public void Clean_TrimsAntiPrompt_TripleNewline()
    {
        var input = "Here is my answer\n\n\n";
        var result = ContentCleaner.Clean(input);
        Assert.Equal("Here is my answer", result);
    }

    [Fact]
    public void Clean_TrimsAntiPrompt_HashSymbols()
    {
        var input = "Here is my answer ###";
        var result = ContentCleaner.Clean(input);
        Assert.Equal("Here is my answer", result);
    }

    [Fact]
    public void Clean_PreservesNormalContent()
    {
        var input = "The weather today is sunny with a high of 75F.";
        Assert.Equal(input, ContentCleaner.Clean(input));
    }

    [Fact]
    public void Clean_HandlesMultipleThinkTags()
    {
        var input = "<think>first thought</think>Answer part 1. <think>second thought</think>Answer part 2.";
        var result = ContentCleaner.Clean(input);
        Assert.Equal("Answer part 1. Answer part 2.", result);
    }

    [Fact]
    public void Clean_TrimsTrailingWhitespace()
    {
        var input = "Hello world   ";
        Assert.Equal("Hello world", ContentCleaner.Clean(input));
    }
}
