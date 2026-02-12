using DaisiBot.Agent.Processing;
using DaisiBot.Core.Enums;

namespace DaisiBot.Agent.Tests.Processing;

public class PlanParserTests
{
    [Fact]
    public void Parse_ValidPlan_ReturnsActionPlan()
    {
        var input = """
            <plan>
            <goal>Search for weather information</goal>
            <step>Look up the current temperature</step>
            <step>Check the forecast for tomorrow</step>
            </plan>
            """;

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal("Search for weather information", result.Goal);
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal(1, result.Steps[0].StepNumber);
        Assert.Equal("Look up the current temperature", result.Steps[0].Description);
        Assert.Equal(2, result.Steps[1].StepNumber);
        Assert.Equal("Check the forecast for tomorrow", result.Steps[1].Description);
    }

    [Fact]
    public void Parse_ValidPlan_AllStepsPending()
    {
        var input = """
            <plan>
            <goal>Do something</goal>
            <step>Step one</step>
            <step>Step two</step>
            </plan>
            """;

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.All(result.Steps, s => Assert.Equal(ActionItemStatus.Pending, s.Status));
    }

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
        Assert.Null(PlanParser.Parse(null!));
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsNull()
    {
        Assert.Null(PlanParser.Parse(""));
    }

    [Fact]
    public void Parse_WhitespaceInput_ReturnsNull()
    {
        Assert.Null(PlanParser.Parse("   "));
    }

    [Fact]
    public void Parse_NoPlanTags_ReturnsNull()
    {
        Assert.Null(PlanParser.Parse("Just some random text without plan tags."));
    }

    [Fact]
    public void Parse_PlanTagsNoGoal_ReturnsNull()
    {
        var input = """
            <plan>
            <step>Do something</step>
            </plan>
            """;

        Assert.Null(PlanParser.Parse(input));
    }

    [Fact]
    public void Parse_PlanTagsEmptyGoal_ReturnsNull()
    {
        var input = """
            <plan>
            <goal>   </goal>
            <step>Do something</step>
            </plan>
            """;

        Assert.Null(PlanParser.Parse(input));
    }

    [Fact]
    public void Parse_GoalButNoSteps_ReturnsNull()
    {
        var input = """
            <plan>
            <goal>Do something</goal>
            </plan>
            """;

        Assert.Null(PlanParser.Parse(input));
    }

    [Fact]
    public void Parse_FiveSteps_ReturnsAllFive()
    {
        var input = """
            <plan>
            <goal>Complex task</goal>
            <step>Step 1</step>
            <step>Step 2</step>
            <step>Step 3</step>
            <step>Step 4</step>
            <step>Step 5</step>
            </plan>
            """;

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(5, result.Steps.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i + 1, result.Steps[i].StepNumber);
        }
    }

    [Fact]
    public void Parse_MoreThanFiveSteps_CapsAtFive()
    {
        var input = """
            <plan>
            <goal>Overly complex task</goal>
            <step>Step 1</step>
            <step>Step 2</step>
            <step>Step 3</step>
            <step>Step 4</step>
            <step>Step 5</step>
            <step>Step 6</step>
            <step>Step 7</step>
            </plan>
            """;

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(5, result.Steps.Count);
        Assert.Equal("Step 5", result.Steps[4].Description);
    }

    [Fact]
    public void Parse_SingleStep_ReturnsPlanWithOneStep()
    {
        var input = """
            <plan>
            <goal>Simple task</goal>
            <step>Just do this one thing</step>
            </plan>
            """;

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.Single(result.Steps);
        Assert.Equal("Just do this one thing", result.Steps[0].Description);
    }

    [Fact]
    public void Parse_PlanWithSurroundingText_ExtractsPlan()
    {
        var input = """
            Here is my analysis of the request.

            <plan>
            <goal>Help the user</goal>
            <step>First action</step>
            <step>Second action</step>
            </plan>

            I hope this plan works well.
            """;

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal("Help the user", result.Goal);
        Assert.Equal(2, result.Steps.Count);
    }

    [Fact]
    public void Parse_PlanWithEmptySteps_SkipsEmptySteps()
    {
        var input = """
            <plan>
            <goal>Task with gaps</goal>
            <step>Real step</step>
            <step>   </step>
            <step>Another real step</step>
            </plan>
            """;

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal("Real step", result.Steps[0].Description);
        Assert.Equal("Another real step", result.Steps[1].Description);
        Assert.Equal(1, result.Steps[0].StepNumber);
        Assert.Equal(2, result.Steps[1].StepNumber);
    }

    [Fact]
    public void Parse_PlanOnSingleLine_Works()
    {
        var input = "<plan><goal>Quick task</goal><step>Do it</step></plan>";

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal("Quick task", result.Goal);
        Assert.Single(result.Steps);
    }

    [Fact]
    public void Parse_StepDescriptionsPreserveContent()
    {
        var input = """
            <plan>
            <goal>Complex descriptions</goal>
            <step>Search for "weather" using the information tool</step>
            <step>Calculate 2 + 2 using math</step>
            </plan>
            """;

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal("Search for \"weather\" using the information tool", result.Steps[0].Description);
        Assert.Equal("Calculate 2 + 2 using math", result.Steps[1].Description);
    }

    [Fact]
    public void Parse_MultilineGoal_TrimsWhitespace()
    {
        var input = """
            <plan>
            <goal>
                Help the user find information
            </goal>
            <step>Search</step>
            </plan>
            """;

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal("Help the user find information", result.Goal);
    }

    [Fact]
    public void Parse_AllEmptySteps_ReturnsNull()
    {
        var input = """
            <plan>
            <goal>Nothing to do</goal>
            <step>   </step>
            <step></step>
            </plan>
            """;

        Assert.Null(PlanParser.Parse(input));
    }

    [Fact]
    public void Parse_StepNumbersAreSequential()
    {
        var input = """
            <plan>
            <goal>Test numbering</goal>
            <step>Alpha</step>
            <step>Beta</step>
            <step>Gamma</step>
            </plan>
            """;

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(1, result.Steps[0].StepNumber);
        Assert.Equal(2, result.Steps[1].StepNumber);
        Assert.Equal(3, result.Steps[2].StepNumber);
    }

    [Fact]
    public void Parse_ResultAndErrorAreNullByDefault()
    {
        var input = """
            <plan>
            <goal>Check defaults</goal>
            <step>Do something</step>
            </plan>
            """;

        var result = PlanParser.Parse(input);

        Assert.NotNull(result);
        Assert.Null(result.Steps[0].Result);
        Assert.Null(result.Steps[0].Error);
    }

    // --- Fallback parser tests ---

    [Fact]
    public void ParseFallback_NumberedList_ReturnsPlan()
    {
        var input = """
            1. Search for the latest news
            2. Summarize the results
            3. Format the output
            """;

        var result = PlanParser.ParseFallback(input, "Get the news");

        Assert.NotNull(result);
        Assert.Equal("Get the news", result.Goal);
        Assert.Equal(3, result.Steps.Count);
        Assert.Equal("Search for the latest news", result.Steps[0].Description);
        Assert.Equal("Summarize the results", result.Steps[1].Description);
        Assert.Equal("Format the output", result.Steps[2].Description);
    }

    [Fact]
    public void ParseFallback_NumberedListWithParens_ReturnsPlan()
    {
        var input = """
            1) First step
            2) Second step
            """;

        var result = PlanParser.ParseFallback(input, "Do task");

        Assert.NotNull(result);
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal("First step", result.Steps[0].Description);
    }

    [Fact]
    public void ParseFallback_BulletList_ReturnsPlan()
    {
        var input = """
            - Search the web
            - Extract data
            - Save results
            """;

        var result = PlanParser.ParseFallback(input, "Research task");

        Assert.NotNull(result);
        Assert.Equal(3, result.Steps.Count);
        Assert.Equal("Search the web", result.Steps[0].Description);
    }

    [Fact]
    public void ParseFallback_MixedWithProse_ExtractsSteps()
    {
        var input = """
            Here's my plan to accomplish the goal:

            1. Search for weather data
            2. Parse the results
            3. Generate a report

            This should cover everything needed.
            """;

        var result = PlanParser.ParseFallback(input, "Weather report");

        Assert.NotNull(result);
        Assert.Equal(3, result.Steps.Count);
    }

    [Fact]
    public void ParseFallback_MoreThanFive_CapsAtFive()
    {
        var input = """
            1. Step one
            2. Step two
            3. Step three
            4. Step four
            5. Step five
            6. Step six
            7. Step seven
            """;

        var result = PlanParser.ParseFallback(input, "Big task");

        Assert.NotNull(result);
        Assert.Equal(5, result.Steps.Count);
    }

    [Fact]
    public void ParseFallback_EmptyInput_ReturnsNull()
    {
        Assert.Null(PlanParser.ParseFallback("", "goal"));
    }

    [Fact]
    public void ParseFallback_NoListItems_ReturnsNull()
    {
        var input = "I'm not sure how to plan this. Let me think about it.";

        Assert.Null(PlanParser.ParseFallback(input, "goal"));
    }

    [Fact]
    public void ParseFallback_StepNumbersAreSequential()
    {
        var input = """
            1. Alpha
            2. Beta
            3. Gamma
            """;

        var result = PlanParser.ParseFallback(input, "Test");

        Assert.NotNull(result);
        Assert.Equal(1, result.Steps[0].StepNumber);
        Assert.Equal(2, result.Steps[1].StepNumber);
        Assert.Equal(3, result.Steps[2].StepNumber);
    }
}
