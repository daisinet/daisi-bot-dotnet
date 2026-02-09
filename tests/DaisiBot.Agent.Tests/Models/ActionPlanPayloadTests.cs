using System.Text.Json;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Models;

namespace DaisiBot.Agent.Tests.Models;

public class ActionPlanPayloadTests
{
    // ── ActionPlanPayload ──

    [Fact]
    public void ActionPlanPayload_Serialization_RoundTrips()
    {
        var payload = new ActionPlanPayload
        {
            Goal = "Help the user",
            Steps =
            [
                new ActionPlanStepPayload { StepNumber = 1, Description = "First step" },
                new ActionPlanStepPayload { StepNumber = 2, Description = "Second step" }
            ]
        };

        var json = JsonSerializer.Serialize(payload);
        var deserialized = JsonSerializer.Deserialize<ActionPlanPayload>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Help the user", deserialized.Goal);
        Assert.Equal(2, deserialized.Steps.Count);
        Assert.Equal(1, deserialized.Steps[0].StepNumber);
        Assert.Equal("First step", deserialized.Steps[0].Description);
        Assert.Equal(2, deserialized.Steps[1].StepNumber);
        Assert.Equal("Second step", deserialized.Steps[1].Description);
    }

    [Fact]
    public void ActionPlanPayload_Serialization_UsesCamelCase()
    {
        var payload = new ActionPlanPayload
        {
            Goal = "Test",
            Steps = [new ActionPlanStepPayload { StepNumber = 1, Description = "Step" }]
        };

        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"goal\"", json);
        Assert.Contains("\"steps\"", json);
        Assert.Contains("\"stepNumber\"", json);
        Assert.Contains("\"description\"", json);
    }

    // ── ActionStepStartPayload ──

    [Fact]
    public void ActionStepStartPayload_Serialization_RoundTrips()
    {
        var payload = new ActionStepStartPayload { StepNumber = 3 };

        var json = JsonSerializer.Serialize(payload);
        var deserialized = JsonSerializer.Deserialize<ActionStepStartPayload>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.StepNumber);
    }

    [Fact]
    public void ActionStepStartPayload_Serialization_UsesCamelCase()
    {
        var payload = new ActionStepStartPayload { StepNumber = 1 };
        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"stepNumber\"", json);
    }

    // ── ActionStepResultPayload ──

    [Fact]
    public void ActionStepResultPayload_Serialization_RoundTrips()
    {
        var payload = new ActionStepResultPayload
        {
            StepNumber = 2,
            Status = ActionItemStatus.Complete,
            Summary = "Step completed successfully"
        };

        var json = JsonSerializer.Serialize(payload);
        var deserialized = JsonSerializer.Deserialize<ActionStepResultPayload>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.StepNumber);
        Assert.Equal(ActionItemStatus.Complete, deserialized.Status);
        Assert.Equal("Step completed successfully", deserialized.Summary);
    }

    [Fact]
    public void ActionStepResultPayload_FailedStatus_Serializes()
    {
        var payload = new ActionStepResultPayload
        {
            StepNumber = 1,
            Status = ActionItemStatus.Failed,
            Summary = "Connection timed out"
        };

        var json = JsonSerializer.Serialize(payload);
        var deserialized = JsonSerializer.Deserialize<ActionStepResultPayload>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(ActionItemStatus.Failed, deserialized.Status);
    }

    [Fact]
    public void ActionStepResultPayload_Serialization_UsesCamelCase()
    {
        var payload = new ActionStepResultPayload
        {
            StepNumber = 1,
            Status = ActionItemStatus.Pending,
            Summary = "test"
        };
        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"stepNumber\"", json);
        Assert.Contains("\"status\"", json);
        Assert.Contains("\"summary\"", json);
    }

    // ── ActionPlan / ActionItem models ──

    [Fact]
    public void ActionPlan_DefaultsToEmptySteps()
    {
        var plan = new ActionPlan();

        Assert.Equal(string.Empty, plan.Goal);
        Assert.NotNull(plan.Steps);
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void ActionItem_DefaultsToPendingStatus()
    {
        var item = new ActionItem();

        Assert.Equal(ActionItemStatus.Pending, item.Status);
        Assert.Null(item.Result);
        Assert.Null(item.Error);
    }

    [Fact]
    public void ActionItem_CanTransitionStatuses()
    {
        var item = new ActionItem
        {
            StepNumber = 1,
            Description = "Do something"
        };

        Assert.Equal(ActionItemStatus.Pending, item.Status);

        item.Status = ActionItemStatus.Running;
        Assert.Equal(ActionItemStatus.Running, item.Status);

        item.Status = ActionItemStatus.Complete;
        item.Result = "Done";
        Assert.Equal(ActionItemStatus.Complete, item.Status);
        Assert.Equal("Done", item.Result);
    }

    [Fact]
    public void ActionItem_FailedWithError()
    {
        var item = new ActionItem { StepNumber = 2, Description = "Failing step" };

        item.Status = ActionItemStatus.Failed;
        item.Error = "Network error";

        Assert.Equal(ActionItemStatus.Failed, item.Status);
        Assert.Equal("Network error", item.Error);
        Assert.Null(item.Result);
    }

    // ── Cross-cutting: ChatPanel deserialization simulation ──

    [Fact]
    public void ChatPanel_CanDeserializeActionPlanFromService()
    {
        // Simulate what the service sends
        var payload = new ActionPlanPayload
        {
            Goal = "Find weather info",
            Steps =
            [
                new ActionPlanStepPayload { StepNumber = 1, Description = "Search for location" },
                new ActionPlanStepPayload { StepNumber = 2, Description = "Get forecast" }
            ]
        };
        var json = JsonSerializer.Serialize(payload);

        // Simulate what ChatPanel does
        var deserialized = JsonSerializer.Deserialize<ActionPlanPayload>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Find weather info", deserialized.Goal);
        Assert.Equal(2, deserialized.Steps.Count);
    }

    [Fact]
    public void ChatPanel_CanDeserializeStepStart()
    {
        var json = JsonSerializer.Serialize(new ActionStepStartPayload { StepNumber = 1 });
        var deserialized = JsonSerializer.Deserialize<ActionStepStartPayload>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(1, deserialized.StepNumber);
    }

    [Fact]
    public void ChatPanel_CanDeserializeStepResult()
    {
        var json = JsonSerializer.Serialize(new ActionStepResultPayload
        {
            StepNumber = 2,
            Status = ActionItemStatus.Complete,
            Summary = "Found 5 results"
        });

        var deserialized = JsonSerializer.Deserialize<ActionStepResultPayload>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.StepNumber);
        Assert.Equal(ActionItemStatus.Complete, deserialized.Status);
        Assert.Equal("Found 5 results", deserialized.Summary);
    }
}
