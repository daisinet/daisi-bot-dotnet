using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Daisi.Protos.V1;
using Daisi.SDK.Clients.V1.Host;
using Daisi.SDK.Clients.V1.Orc;
using Daisi.SDK.Clients.V1.SessionManagers;
using DaisiBot.Agent.Auth;
using DaisiBot.Agent.Mapping;
using DaisiBot.Agent.Processing;
using DaisiBot.Agent.Skills;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DaisiBot.Agent.Bots;

public class BotEngine : IBotEngine, IDisposable
{
    private readonly IBotStore _botStore;
    private readonly ISettingsService _settingsService;
    private readonly ISkillService _skillService;
    private readonly DaisiBotClientKeyProvider _keyProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<BotEngine> _logger;
    private readonly ConcurrentDictionary<Guid, BotRuntime> _runtimes = new();
    private readonly Timer _schedulerTimer;

    public event EventHandler<BotInstance>? BotStatusChanged;

    public BotEngine(
        IBotStore botStore,
        ISettingsService settingsService,
        ISkillService skillService,
        DaisiBotClientKeyProvider keyProvider,
        ILoggerFactory loggerFactory)
    {
        _botStore = botStore;
        _settingsService = settingsService;
        _skillService = skillService;
        _keyProvider = keyProvider;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<BotEngine>();

        _schedulerTimer = new Timer(OnSchedulerTick, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));
    }

    public async Task StartBotAsync(Guid botId)
    {
        var bot = await _botStore.GetAsync(botId);
        if (bot is null) return;

        if (_runtimes.ContainsKey(botId))
        {
            _logger.LogWarning("Bot {BotId} is already running", botId);
            return;
        }

        bot.Status = BotStatus.Running;
        bot.NextRunAt = DateTime.UtcNow;
        await _botStore.UpdateAsync(bot);

        var cts = new CancellationTokenSource();
        var outputChannel = Channel.CreateUnbounded<BotLogEntry>();
        var runtime = new BotRuntime(cts, outputChannel);
        _runtimes[botId] = runtime;

        BotStatusChanged?.Invoke(this, bot);

        runtime.ExecutionTask = Task.Run(() => ExecuteBotAsync(botId, runtime, cts.Token));
    }

    public async Task StopBotAsync(Guid botId)
    {
        if (_runtimes.TryRemove(botId, out var runtime))
        {
            await runtime.Cts.CancelAsync();
        }

        var bot = await _botStore.GetAsync(botId);
        if (bot is null) return;

        bot.Status = BotStatus.Stopped;
        bot.PendingQuestion = null;
        bot.NextRunAt = null;
        await _botStore.UpdateAsync(bot);

        BotStatusChanged?.Invoke(this, bot);
    }

    public async Task SendInputAsync(Guid botId, string userInput)
    {
        if (!_runtimes.TryGetValue(botId, out var runtime) || runtime.InputTcs is null)
            return;

        var bot = await _botStore.GetAsync(botId);
        if (bot is not null)
        {
            await _botStore.AddLogEntryAsync(new BotLogEntry
            {
                BotId = botId,
                ExecutionNumber = bot.ExecutionCount,
                Level = BotLogLevel.UserResponse,
                Message = userInput
            });
        }

        runtime.InputTcs.TrySetResult(userInput);
    }

    public bool IsRunning(Guid botId) => _runtimes.ContainsKey(botId);

    private async Task ExecuteBotAsync(Guid botId, BotRuntime runtime, CancellationToken ct)
    {
        try
        {
            var bot = await _botStore.GetAsync(botId);
            if (bot is null) return;

            bot.ExecutionCount++;
            bot.LastRunAt = DateTime.UtcNow;
            await _botStore.UpdateAsync(bot);

            await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Info, $"Starting bot: {bot.Label}");
            await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Info, $"Goal: {bot.Goal}");

            if (!string.IsNullOrWhiteSpace(bot.RetryGuidance))
                await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Warning,
                    "Retrying with guidance", bot.RetryGuidance);

            // Step 1: Skill assessment
            await LogAsync(botId, bot.ExecutionCount, BotLogLevel.StepStart, "Assessing required skills...");
            var settings = await _settingsService.GetSettingsAsync();

            // Step 2: Plan
            await LogAsync(botId, bot.ExecutionCount, BotLogLevel.StepStart, "Creating execution plan...");

            var inferenceClient = CreateInferenceClient();
            try
            {
                var plan = await CreatePlanAsync(inferenceClient, bot, settings, ct);
                if (plan is null)
                {
                    await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Warning, "Could not create plan, executing goal directly");
                    await ExecuteDirectAsync(inferenceClient, bot, settings, runtime, ct);
                }
                else
                {
                    await LogAsync(botId, bot.ExecutionCount, BotLogLevel.StepComplete,
                        $"Plan created: {plan.Goal} ({plan.Steps.Count} steps)");

                    // Step 3: Execute plan steps
                    await ExecutePlanStepsAsync(inferenceClient, bot, plan, settings, runtime, ct);

                    // Step 4: Synthesize
                    await LogAsync(botId, bot.ExecutionCount, BotLogLevel.StepStart, "Synthesizing results...");
                    // Synthesis is done within ExecutePlanStepsAsync
                }
            }
            finally
            {
                try { await inferenceClient.CloseAsync(); } catch { }
            }

            // Reschedule
            bot = await _botStore.GetAsync(botId);
            if (bot is null) return;

            // Clear retry guidance after a successful execution
            bot.RetryGuidance = null;
            bot.LastError = null;

            if (ct.IsCancellationRequested)
            {
                bot.Status = BotStatus.Stopped;
                bot.NextRunAt = null;
            }
            else
            {
                ComputeNextRun(bot);
            }

            await _botStore.UpdateAsync(bot);
            await LogAsync(botId, bot.ExecutionCount, BotLogLevel.StepComplete,
                $"Execution complete. Status: {bot.Status}");

            BotStatusChanged?.Invoke(this, bot);
        }
        catch (OperationCanceledException)
        {
            await SetBotStatusAsync(botId, BotStatus.Stopped, "Cancelled by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot {BotId} failed", botId);
            await HandleBotFailureAsync(botId, runtime, ex);
        }
        finally
        {
            _runtimes.TryRemove(botId, out _);
        }
    }

    private async Task<ActionPlan?> CreatePlanAsync(
        InferenceClient client, BotInstance bot, UserSettings settings, CancellationToken ct)
    {
        var planRequest = new CreateInferenceRequest
        {
            ModelName = !string.IsNullOrWhiteSpace(bot.ModelName) ? bot.ModelName : settings.DefaultModelName,
            InitializationPrompt = BuildBotPlanningPrompt(bot),
            ThinkLevel = ThinkLevels.Basic
        };

        try
        {
            await client.CreateAsync(planRequest);

            var sendRequest = SendInferenceRequest.CreateDefault();
            sendRequest.Text = bot.Goal;
            sendRequest.Temperature = 0.3f;
            sendRequest.MaxTokens = 1024;
            sendRequest.ThinkLevel = ThinkLevels.Basic;

            var content = new StringBuilder();
            var stream = client.Send(sendRequest);
            while (await stream.ResponseStream.MoveNext(ct))
            {
                content.Append(stream.ResponseStream.Current.Content);
            }

            try { await client.CloseAsync(closeOrcSession: false); } catch { }
            return PlanParser.Parse(content.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planning failed for bot {BotId}", bot.Id);
            try { await client.CloseAsync(closeOrcSession: false); } catch { }
            return null;
        }
    }

    private async Task ExecutePlanStepsAsync(
        InferenceClient client, BotInstance bot, ActionPlan plan,
        UserSettings settings, BotRuntime runtime, CancellationToken ct)
    {
        var modelName = !string.IsNullOrWhiteSpace(bot.ModelName) ? bot.ModelName : settings.DefaultModelName;

        // Create execution session
        var execRequest = new CreateInferenceRequest
        {
            ModelName = modelName,
            InitializationPrompt = BuildBotExecutionPrompt(bot),
            ThinkLevel = ThinkLevels.BasicWithTools
        };
        foreach (var group in settings.GetEnabledToolGroups())
            execRequest.ToolGroups.Add(EnumMapper.ToProtoToolGroup(group));

        await client.CreateAsync(execRequest);

        var stepResults = new List<string>();
        foreach (var step in plan.Steps)
        {
            if (ct.IsCancellationRequested) break;

            await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.StepStart,
                $"Step {step.StepNumber}: {step.Description}");

            var stepContent = new StringBuilder();
            var stepPrompt = BuildStepPrompt(step, stepResults, bot.Goal);

            try
            {
                var sendRequest = SendInferenceRequest.CreateDefault();
                sendRequest.Text = stepPrompt;
                sendRequest.Temperature = bot.Temperature;
                sendRequest.MaxTokens = bot.MaxTokens;
                sendRequest.ThinkLevel = ThinkLevels.BasicWithTools;

                var stream = client.Send(sendRequest);
                while (await stream.ResponseStream.MoveNext(ct))
                {
                    var chunk = stream.ResponseStream.Current;
                    if (chunk.Type is InferenceResponseTypes.Text or InferenceResponseTypes.ToolContent)
                        stepContent.Append(chunk.Content);
                }

                var result = stepContent.ToString().Trim();
                step.Status = ActionItemStatus.Complete;
                step.Result = result;
                stepResults.Add($"Step {step.StepNumber}: {result}");

                await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.StepComplete,
                    $"Step {step.StepNumber} complete", result.Length > 500 ? result[..500] : result);
            }
            catch (Exception ex)
            {
                step.Status = ActionItemStatus.Failed;
                step.Error = ex.Message;
                stepResults.Add($"Step {step.StepNumber} failed: {ex.Message}");

                await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Error,
                    $"Step {step.StepNumber} failed: {ex.Message}");
            }
        }

        try { await client.CloseAsync(closeOrcSession: false); } catch { }

        // Synthesis
        if (stepResults.Count > 0 && !ct.IsCancellationRequested)
        {
            try
            {
                var synthRequest = new CreateInferenceRequest
                {
                    ModelName = modelName,
                    InitializationPrompt = "You are a helpful assistant. Synthesize the results below into a clear summary.",
                    ThinkLevel = ThinkLevels.Basic
                };
                await client.CreateAsync(synthRequest);

                var synthSend = SendInferenceRequest.CreateDefault();
                synthSend.Text = $"Goal: {bot.Goal}\n\nResults:\n{string.Join("\n", stepResults)}\n\nProvide a concise summary.";
                synthSend.Temperature = 0.5f;
                synthSend.MaxTokens = bot.MaxTokens;

                var synthContent = new StringBuilder();
                var synthStream = client.Send(synthSend);
                while (await synthStream.ResponseStream.MoveNext(ct))
                {
                    synthContent.Append(synthStream.ResponseStream.Current.Content);
                }

                await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Info,
                    "Summary: " + synthContent.ToString().Trim());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Synthesis failed for bot {BotId}", bot.Id);
            }
        }
    }

    private async Task ExecuteDirectAsync(
        InferenceClient client, BotInstance bot, UserSettings settings,
        BotRuntime runtime, CancellationToken ct)
    {
        var modelName = !string.IsNullOrWhiteSpace(bot.ModelName) ? bot.ModelName : settings.DefaultModelName;

        var createRequest = new CreateInferenceRequest
        {
            ModelName = modelName,
            InitializationPrompt = BuildBotExecutionPrompt(bot),
            ThinkLevel = ThinkLevels.BasicWithTools
        };
        foreach (var group in settings.GetEnabledToolGroups())
            createRequest.ToolGroups.Add(EnumMapper.ToProtoToolGroup(group));

        await client.CreateAsync(createRequest);

        var sendRequest = SendInferenceRequest.CreateDefault();
        sendRequest.Text = bot.Goal;
        sendRequest.Temperature = bot.Temperature;
        sendRequest.MaxTokens = bot.MaxTokens;
        sendRequest.ThinkLevel = ThinkLevels.BasicWithTools;

        var content = new StringBuilder();
        var stream = client.Send(sendRequest);
        while (await stream.ResponseStream.MoveNext(ct))
        {
            var chunk = stream.ResponseStream.Current;
            if (chunk.Type is InferenceResponseTypes.Text or InferenceResponseTypes.ToolContent)
                content.Append(chunk.Content);
        }

        await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Info,
            "Result: " + content.ToString().Trim());
    }

    public async Task<string> WaitForInputAsync(Guid botId, string question, CancellationToken ct)
    {
        var bot = await _botStore.GetAsync(botId);
        if (bot is null) throw new InvalidOperationException("Bot not found");

        bot.Status = BotStatus.WaitingForInput;
        bot.PendingQuestion = question;
        await _botStore.UpdateAsync(bot);

        await LogAsync(botId, bot.ExecutionCount, BotLogLevel.UserPrompt, question);
        BotStatusChanged?.Invoke(this, bot);

        if (!_runtimes.TryGetValue(botId, out var runtime))
            throw new InvalidOperationException("Bot runtime not found");

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.InputTcs = tcs;

        using var reg = ct.Register(() => tcs.TrySetCanceled());
        var result = await tcs.Task;

        runtime.InputTcs = null;

        bot = await _botStore.GetAsync(botId);
        if (bot is not null)
        {
            bot.Status = BotStatus.Running;
            bot.PendingQuestion = null;
            await _botStore.UpdateAsync(bot);
            BotStatusChanged?.Invoke(this, bot);
        }

        return result;
    }

    private async void OnSchedulerTick(object? state)
    {
        try
        {
            var runnable = await _botStore.GetRunnableAsync();
            foreach (var bot in runnable)
            {
                if (!_runtimes.ContainsKey(bot.Id))
                {
                    _ = StartBotAsync(bot.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduler tick failed");
        }
    }

    private void ComputeNextRun(BotInstance bot)
    {
        switch (bot.ScheduleType)
        {
            case BotScheduleType.Once:
                bot.Status = BotStatus.Completed;
                bot.NextRunAt = null;
                break;
            case BotScheduleType.Continuous:
                bot.Status = BotStatus.Running;
                bot.NextRunAt = DateTime.UtcNow;
                break;
            case BotScheduleType.Interval:
                bot.Status = BotStatus.Running;
                bot.NextRunAt = DateTime.UtcNow.AddMinutes(bot.ScheduleIntervalMinutes);
                break;
            case BotScheduleType.Hourly:
                bot.Status = BotStatus.Running;
                bot.NextRunAt = DateTime.UtcNow.AddHours(1);
                break;
            case BotScheduleType.Daily:
                bot.Status = BotStatus.Running;
                bot.NextRunAt = DateTime.UtcNow.AddDays(1);
                break;
        }
    }

    private async Task HandleBotFailureAsync(Guid botId, BotRuntime runtime, Exception ex)
    {
        var bot = await _botStore.GetAsync(botId);
        if (bot is null) return;

        bot.LastError = ex.Message;
        await _botStore.UpdateAsync(bot);
        await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Error, $"Execution failed: {ex.Message}");

        // Ask the user whether to retry or stop — their response becomes guidance for the next attempt
        try
        {
            var response = await WaitForInputAsync(botId,
                $"Bot failed: {ex.Message}\nReply 'stop' to stop the bot, or provide guidance for the next attempt.",
                CancellationToken.None);

            bot = await _botStore.GetAsync(botId);
            if (bot is null) return;

            if (response.Trim().Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                bot.Status = BotStatus.Stopped;
                bot.NextRunAt = null;
                bot.RetryGuidance = null;
                await _botStore.UpdateAsync(bot);
                await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Info, "Bot stopped by user after failure");
                BotStatusChanged?.Invoke(this, bot);
            }
            else
            {
                bot.RetryGuidance = $"Previous attempt failed: {ex.Message}\nUser guidance: {response.Trim()}";
                ComputeRetryRun(bot);
                await _botStore.UpdateAsync(bot);
                await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Info,
                    $"Retrying with user guidance at {bot.NextRunAt:HH:mm:ss}");
                BotStatusChanged?.Invoke(this, bot);
            }
        }
        catch
        {
            // If waiting for input fails (e.g. runtime removed), schedule retry automatically
            bot = await _botStore.GetAsync(botId);
            if (bot is not null)
            {
                bot.RetryGuidance = $"Previous attempt failed: {ex.Message}";
                ComputeRetryRun(bot);
                await _botStore.UpdateAsync(bot);
                BotStatusChanged?.Invoke(this, bot);
            }
        }
    }

    private void ComputeRetryRun(BotInstance bot)
    {
        bot.Status = BotStatus.Running;
        bot.NextRunAt = bot.ScheduleType switch
        {
            BotScheduleType.Continuous => DateTime.UtcNow.AddSeconds(30),
            BotScheduleType.Once => DateTime.UtcNow.AddMinutes(1),
            BotScheduleType.Interval => DateTime.UtcNow.AddMinutes(bot.ScheduleIntervalMinutes),
            BotScheduleType.Hourly => DateTime.UtcNow.AddHours(1),
            BotScheduleType.Daily => DateTime.UtcNow.AddDays(1),
            _ => DateTime.UtcNow.AddMinutes(1)
        };
    }

    private async Task SetBotStatusAsync(Guid botId, BotStatus status, string? error = null)
    {
        var bot = await _botStore.GetAsync(botId);
        if (bot is null) return;

        bot.Status = status;
        bot.LastError = error;
        bot.PendingQuestion = null;
        await _botStore.UpdateAsync(bot);

        if (error is not null)
            await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Error, error);

        BotStatusChanged?.Invoke(this, bot);
    }

    private async Task LogAsync(Guid botId, int executionNumber, BotLogLevel level, string message, string? detail = null)
    {
        var entry = new BotLogEntry
        {
            BotId = botId,
            ExecutionNumber = executionNumber,
            Level = level,
            Message = message,
            Detail = detail
        };

        try
        {
            await _botStore.AddLogEntryAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist bot log entry");
        }

        if (_runtimes.TryGetValue(botId, out var runtime))
        {
            runtime.OutputChannel.Writer.TryWrite(entry);
        }
    }

    private InferenceClient CreateInferenceClient()
    {
        var sessionClientFactory = new SessionClientFactory(_keyProvider);
        var sessionManager = new InferenceSessionManager(
            sessionClientFactory,
            _keyProvider,
            _loggerFactory.CreateLogger<InferenceSessionManager>());
        var factory = new InferenceClientFactory(sessionManager);
        return factory.Create();
    }

    private static string BuildBotPlanningPrompt(BotInstance bot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an autonomous bot task planner.");
        if (!string.IsNullOrWhiteSpace(bot.Persona))
            sb.AppendLine($"Persona: {bot.Persona}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(bot.RetryGuidance))
        {
            sb.AppendLine("IMPORTANT — This is a retry after a previous failure. Adjust your plan accordingly:");
            sb.AppendLine(bot.RetryGuidance);
            sb.AppendLine();
        }

        sb.AppendLine("""
            Given a goal, break it down into simple steps.

            Rules:
            - Each step must be a single, specific action
            - Use 2 to 5 steps maximum
            - Do NOT include a "summarize" step - that happens automatically
            - Output ONLY the plan in the exact format below

            Format:
            <plan>
            <goal>One sentence describing the goal</goal>
            <step>First thing to do</step>
            <step>Second thing to do</step>
            </plan>
            """);
        return sb.ToString();
    }

    private static string BuildBotExecutionPrompt(BotInstance bot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an autonomous bot executing a task.");
        if (!string.IsNullOrWhiteSpace(bot.Persona))
            sb.AppendLine($"Persona: {bot.Persona}");
        sb.AppendLine($"Goal: {bot.Goal}");

        if (!string.IsNullOrWhiteSpace(bot.RetryGuidance))
        {
            sb.AppendLine();
            sb.AppendLine("IMPORTANT — This is a retry after a previous failure. Take the following into account:");
            sb.AppendLine(bot.RetryGuidance);
        }

        sb.AppendLine("Complete each step thoroughly and be concise in your output.");
        return sb.ToString();
    }

    private static string BuildStepPrompt(ActionItem step, List<string> priorResults, string goal)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Goal: {goal}");
        sb.AppendLine();

        if (priorResults.Count > 0)
        {
            sb.AppendLine("Previous results:");
            foreach (var result in priorResults)
                sb.AppendLine($"- {result}");
            sb.AppendLine();
        }

        sb.AppendLine($"Current task (Step {step.StepNumber}): {step.Description}");
        sb.AppendLine("Complete this step. Be concise and specific.");
        return sb.ToString();
    }

    public void Dispose()
    {
        _schedulerTimer.Dispose();
        foreach (var kvp in _runtimes)
        {
            kvp.Value.Cts.Cancel();
            kvp.Value.Cts.Dispose();
        }
        _runtimes.Clear();
    }

    private class BotRuntime(CancellationTokenSource cts, Channel<BotLogEntry> outputChannel)
    {
        public CancellationTokenSource Cts { get; } = cts;
        public Channel<BotLogEntry> OutputChannel { get; } = outputChannel;
        public Task? ExecutionTask { get; set; }
        public TaskCompletionSource<string>? InputTcs { get; set; }
    }
}
