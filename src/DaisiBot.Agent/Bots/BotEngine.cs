using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
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
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace DaisiBot.Agent.Bots;

public class BotEngine : IBotEngine, IDisposable
{
    private readonly IBotStore _botStore;
    private readonly ISettingsService _settingsService;
    private readonly ISkillService _skillService;
    private readonly ISkillFileLoader _skillFileLoader;
    private readonly DaisiBotClientKeyProvider _keyProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<BotEngine> _logger;
    private readonly ILocalInferenceService? _localInference;
    private readonly ConcurrentDictionary<Guid, BotRuntime> _runtimes = new();
    private readonly Timer _schedulerTimer;

    public event EventHandler<BotInstance>? BotStatusChanged;
    public event EventHandler<ActionPlanChangedEventArgs>? ActionPlanChanged;
    public event EventHandler<BotLogEntry>? BotLogEntryAdded;
    public event EventHandler<Guid>? AuthenticationRequired;

    public BotEngine(
        IBotStore botStore,
        ISettingsService settingsService,
        ISkillService skillService,
        ISkillFileLoader skillFileLoader,
        DaisiBotClientKeyProvider keyProvider,
        ILoggerFactory loggerFactory,
        ILocalInferenceService? localInference = null)
    {
        _botStore = botStore;
        _settingsService = settingsService;
        _skillService = skillService;
        _skillFileLoader = skillFileLoader;
        _keyProvider = keyProvider;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<BotEngine>();
        _localInference = localInference;

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

        runtime.ExecutionTask = Task.Run(() => ExecuteBotLoopAsync(botId, runtime, cts.Token));
    }

    public async Task StopAllBotsAsync()
    {
        var runningIds = _runtimes.Keys.ToList();
        foreach (var id in runningIds)
            await StopBotAsync(id);
    }

    public async Task RestartAllBotsAsync()
    {
        var runningIds = _runtimes.Keys.ToList();
        foreach (var id in runningIds)
            await StopBotAsync(id);
        foreach (var id in runningIds)
            await StartBotAsync(id);
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

        if (_runtimes.TryGetValue(botId, out var runtime))
            runtime.UserMessages.Enqueue(userInput);
    }

    public bool IsRunning(Guid botId) => _runtimes.ContainsKey(botId);

    /// <summary>
    /// Long-running loop that keeps the runtime alive across execution cycles.
    /// User messages can be queued at any time and are drained at the start of each cycle.
    /// </summary>
    private async Task ExecuteBotLoopAsync(Guid botId, BotRuntime runtime, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var bot = await _botStore.GetAsync(botId);
                if (bot is null) break;

                // Sleep until the next scheduled run
                if (bot.NextRunAt.HasValue && bot.NextRunAt.Value > DateTime.UtcNow)
                {
                    var delay = bot.NextRunAt.Value - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        BotStatusChanged?.Invoke(this, bot);
                        await Task.Delay(delay, ct);
                    }
                }

                if (ct.IsCancellationRequested) break;

                // Execute one cycle
                try
                {
                    await ExecuteCycleAsync(botId, runtime, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bot {BotId} cycle failed", botId);
                    await HandleBotFailureAsync(botId, ex);
                }

                // Check whether to continue
                bot = await _botStore.GetAsync(botId);
                if (bot is null) break;
                if (bot.Status is BotStatus.Completed or BotStatus.Stopped) break;
                if (bot.NextRunAt is null) break;
            }
        }
        catch (OperationCanceledException)
        {
            // StopBotAsync already set status to Stopped
        }
        finally
        {
            _runtimes.TryRemove(botId, out _);
        }
    }

    /// <summary>
    /// Executes a single bot cycle: drain user messages, plan, execute steps, reschedule.
    /// </summary>
    private async Task ExecuteCycleAsync(Guid botId, BotRuntime runtime, CancellationToken ct)
    {
        var bot = await _botStore.GetAsync(botId);
        if (bot is null) return;

        // Drain queued user instructions
        var userInstructions = new List<string>();
        while (runtime.UserMessages.TryDequeue(out var msg))
            userInstructions.Add(msg);

        bot.ExecutionCount++;
        bot.LastRunAt = DateTime.UtcNow;
        await _botStore.UpdateAsync(bot);

        var settings = await _settingsService.GetSettingsAsync();

        // Load memories for this bot
        List<BotMemoryEntry> memories = [];
        if (bot.MemoryEnabled)
            memories = await _botStore.GetMemoriesAsync(botId, bot.MaxMemoryEntries);

        // Open log file for this run if file logging is enabled
        if (settings.BotFileLoggingEnabled)
        {
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory, "daisi-bot-logs");
                Directory.CreateDirectory(logDir);
                var safeLabel = SanitizeFileName(bot.Label);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"{safeLabel}_run{bot.ExecutionCount}_{timestamp}.log";
                var writer = new StreamWriter(Path.Combine(logDir, fileName), append: false, System.Text.Encoding.UTF8);
                runtime.LogFileWriter = writer;
                WriteLogFileHeader(writer, bot, settings);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create log file for bot {BotId}", botId);
            }
        }

        try
        {

        var runTime = DateTime.Now.ToString("h:mmtt").ToLower();
        await LogAsync(botId, bot.ExecutionCount, BotLogLevel.RunStart,
            $"Executing Run #{bot.ExecutionCount} at {runTime}");
        await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Info, $"Goal: {bot.Goal}");

        if (userInstructions.Count > 0)
            await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Info,
                $"User instructions ({userInstructions.Count})", string.Join("\n", userInstructions));

        if (!string.IsNullOrWhiteSpace(bot.RetryGuidance))
            await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Warning,
                "Retrying with guidance", bot.RetryGuidance);

        // Step 1: Skill assessment
        await LogAsync(botId, bot.ExecutionCount, BotLogLevel.StepStart, "Assessing required skills...");

        // Resolve skills
        var allSkills = await _skillFileLoader.LoadAllAsync(ct);
        var enabledSkillIds = bot.GetEnabledSkillIds();
        var activeSkills = enabledSkillIds.Count > 0
            ? allSkills.Where(s => enabledSkillIds.Contains(s.Id, StringComparer.OrdinalIgnoreCase)).ToList()
            : allSkills;

        var skillNames = activeSkills.Select(s => s.Name).OrderBy(n => n).ToList();
        var toolGroups = settings.GetEnabledToolGroups();

        var assessmentDetail = new StringBuilder();
        if (skillNames.Count > 0)
        {
            assessmentDetail.AppendLine($"Skills ({skillNames.Count}): {string.Join(", ", skillNames)}");
        }
        else
        {
            assessmentDetail.AppendLine("Skills: none");
        }

        if (toolGroups.Count > 0)
        {
            assessmentDetail.AppendLine($"Tool groups ({toolGroups.Count}): {string.Join(", ", toolGroups)}");
        }
        else
        {
            assessmentDetail.AppendLine("Tool groups: none");
        }

        await LogAsync(botId, bot.ExecutionCount, BotLogLevel.StepComplete,
            $"Skill assessment complete — {skillNames.Count} skills, {toolGroups.Count} tool groups",
            assessmentDetail.ToString().TrimEnd());

        // Step 2: Plan — check for persisted steps first
        await LogAsync(botId, bot.ExecutionCount, BotLogLevel.StepStart, "Creating execution plan...");

        var persistedSteps = await _botStore.GetStepsAsync(botId);
        ActionPlan? prebuiltPlan = null;
        if (persistedSteps.Count > 0)
        {
            prebuiltPlan = new ActionPlan
            {
                Goal = bot.Goal,
                Steps = persistedSteps.Select(s => new ActionItem
                {
                    StepNumber = s.StepNumber,
                    Description = s.Description
                }).ToList()
            };
            await LogAsync(botId, bot.ExecutionCount, BotLogLevel.StepComplete,
                $"Using {persistedSteps.Count} user-defined steps");
        }

        var plan = prebuiltPlan ?? await CreatePlanAsync(bot, settings, userInstructions, memories, ct);
        if (plan is null)
        {
            await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Warning, "Could not create plan, executing goal directly");
            var directResult = await ExecuteDirectAsync(bot, settings, userInstructions, memories, ct);

            // Extract memories after direct execution
            if (bot.MemoryEnabled && !string.IsNullOrWhiteSpace(directResult))
                await ExtractAndStoreMemoriesAsync(bot, settings, directResult, ct);
        }
        else
        {
            if (prebuiltPlan is null)
            {
                await LogAsync(botId, bot.ExecutionCount, BotLogLevel.StepComplete,
                    $"Plan created: {plan.Goal} ({plan.Steps.Count} steps)");
            }

            FireActionPlanChanged(botId, plan);

            // Step 3: Execute plan steps + synthesize
            var synthesisResult = await ExecutePlanStepsAsync(bot, plan, settings, userInstructions, memories, ct);

            // Extract memories after synthesis
            if (bot.MemoryEnabled && !string.IsNullOrWhiteSpace(synthesisResult))
                await ExtractAndStoreMemoriesAsync(bot, settings, synthesisResult, ct);
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

        } // end try
        finally
        {
            // Close log file for this run
            if (runtime.LogFileWriter is { } logWriter)
            {
                try
                {
                    logWriter.WriteLine();
                    logWriter.WriteLine(new string('\u2550', 60));
                    logWriter.WriteLine($"Log closed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    logWriter.Flush();
                    logWriter.Dispose();
                }
                catch { }
                runtime.LogFileWriter = null;
            }
        }
    }

    private async Task<ActionPlan?> CreatePlanAsync(
        BotInstance bot, UserSettings settings,
        List<string> userInstructions, List<BotMemoryEntry> memories, CancellationToken ct)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var client = CreateBotSession(settings);
            try
            {
                var planRequest = new CreateInferenceRequest
                {
                    ModelName = !string.IsNullOrWhiteSpace(bot.ModelName) ? bot.ModelName : settings.DefaultModelName,
                    InitializationPrompt = BuildBotPlanningPrompt(bot, userInstructions, memories),
                    ThinkLevel = ThinkLevels.Basic
                };

                await client.CreateAsync(planRequest);

                var sendRequest = SendInferenceRequest.CreateDefault();
                sendRequest.Text = bot.Goal;
                sendRequest.Temperature = 0.3f;
                sendRequest.MaxTokens = 1024;
                sendRequest.ThinkLevel = ThinkLevels.Basic;
                sendRequest.ExampleOutput = "<plan>\n<goal>Summarize today's headlines</goal>\n<step>Search for today's top news stories</step>\n<step>Read and extract key points from each story</step>\n<step>Compile a concise summary of the headlines</step>\n</plan>";

                var content = new StringBuilder();
                await foreach (var chunk in client.SendAsync(sendRequest, ct))
                {
                    if (chunk.Type is InferenceResponseTypes.Text or InferenceResponseTypes.ToolContent)
                        content.Append(chunk.Content);
                }

                var raw = content.ToString();
                if (settings.LogInferenceOutputEnabled)
                    await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Debug,
                        "Plan inference response", raw);

                if (string.IsNullOrWhiteSpace(raw))
                {
                    if (attempt < maxAttempts)
                    {
                        await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Warning,
                            $"Plan inference returned empty (attempt {attempt}/{maxAttempts}), possible session timeout \u2014 retrying...");
                        continue;
                    }
                    await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Error,
                        $"Plan inference returned empty after {maxAttempts} attempts");
                    return null;
                }

                var plan = PlanParser.Parse(raw);
                if (plan is null && !string.IsNullOrWhiteSpace(raw))
                {
                    await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Warning,
                        "Plan response did not match expected format, trying fallback parser",
                        raw.Length > 500 ? raw[..500] + "..." : raw);
                    plan = PlanParser.ParseFallback(raw, bot.Goal);
                }
                // Fill in goal from bot if parser didn't find one
                if (plan is not null && string.IsNullOrWhiteSpace(plan.Goal))
                    plan.Goal = bot.Goal;
                return plan;
            }
            catch (Exception ex)
            {
                if (IsAuthenticationError(ex)) throw; // bubble up to HandleBotFailureAsync
                var rootMsg = GetRootMessage(ex);
                if (attempt < maxAttempts)
                {
                    await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Warning,
                        $"Plan inference error (attempt {attempt}/{maxAttempts}): {rootMsg} \u2014 retrying...");
                    continue;
                }
                _logger.LogWarning(ex, "Planning failed for bot {BotId}", bot.Id);
                DiagLog($"Bot {bot.Id} planning failed: {UnwrapException(ex)}");
                await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Error,
                    $"Planning failed after {maxAttempts} attempts: {rootMsg}");
                return null;
            }
            finally
            {
                await client.DisposeAsync();
            }
        }

        return null;
    }

    private async Task<string?> ExecutePlanStepsAsync(
        BotInstance bot, ActionPlan plan,
        UserSettings settings, List<string> userInstructions, List<BotMemoryEntry> memories, CancellationToken ct)
    {
        const int maxAttempts = 3;
        var modelName = !string.IsNullOrWhiteSpace(bot.ModelName) ? bot.ModelName : settings.DefaultModelName;

        var stepResults = new List<string>();

        var execRequest = new CreateInferenceRequest
        {
            ModelName = modelName,
            InitializationPrompt = BuildBotExecutionPrompt(bot, userInstructions, memories),
            ThinkLevel = ThinkLevels.BasicWithTools
        };
        foreach (var group in settings.GetEnabledToolGroups())
            execRequest.ToolGroups.Add(EnumMapper.ToProtoToolGroup(group));

        // Execute steps — client is recreated on retry
        var execClient = CreateBotSession(settings);
        try
        {
            await execClient.CreateAsync(execRequest);

            foreach (var step in plan.Steps)
            {
                if (ct.IsCancellationRequested) break;

                step.Status = ActionItemStatus.Running;
                FireActionPlanChanged(bot.Id, plan);

                await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.StepStart,
                    $"Step {step.StepNumber}: {step.Description}");

                var stepPrompt = BuildStepPrompt(step, stepResults, bot.Goal);

                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        if (attempt > 1)
                        {
                            await execClient.DisposeAsync();
                            execClient = CreateBotSession(settings);
                            await execClient.CreateAsync(execRequest);
                        }

                        var stepContent = new StringBuilder();
                        var sendRequest = SendInferenceRequest.CreateDefault();
                        sendRequest.Text = stepPrompt;
                        sendRequest.Temperature = bot.Temperature;
                        sendRequest.MaxTokens = bot.MaxTokens;
                        sendRequest.ThinkLevel = ThinkLevels.BasicWithTools;

                        var toolsUsed = new List<string>();
                        await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Info, "Waiting for inference...");
                        await foreach (var chunk in execClient.SendAsync(sendRequest, ct))
                        {
                            if (chunk.Type == InferenceResponseTypes.Tooling)
                            {
                                toolsUsed.Add(chunk.Content);
                                await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.SkillAction, chunk.Content);
                            }
                            if (chunk.Type is InferenceResponseTypes.Text or InferenceResponseTypes.ToolContent)
                                stepContent.Append(chunk.Content);
                        }

                        var result = stepContent.ToString().Trim();
                        if (settings.LogInferenceOutputEnabled)
                            await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Debug,
                                $"Step {step.StepNumber} inference response", result);

                        if (string.IsNullOrWhiteSpace(result))
                        {
                            if (attempt < maxAttempts)
                            {
                                await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Warning,
                                    $"Step {step.StepNumber} returned empty (attempt {attempt}/{maxAttempts}), possible session timeout \u2014 retrying...");
                                continue;
                            }
                            step.Status = ActionItemStatus.Failed;
                            step.Error = $"Inference returned empty after {maxAttempts} attempts";
                            stepResults.Add($"Step {step.StepNumber} failed: empty response");
                            FireActionPlanChanged(bot.Id, plan);
                            await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Error,
                                $"Step {step.StepNumber} failed: inference returned empty after {maxAttempts} attempts");
                            break;
                        }

                        step.Status = ActionItemStatus.Complete;
                        step.Result = result;
                        stepResults.Add($"Step {step.StepNumber}: {result}");

                        FireActionPlanChanged(bot.Id, plan);

                        var completionMessage = $"Step {step.StepNumber} complete";
                        if (toolsUsed.Count > 0)
                            completionMessage += $" ({toolsUsed.Count} tool call{(toolsUsed.Count == 1 ? "" : "s")})";
                        await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.StepComplete,
                            completionMessage, result);
                        break; // success — exit retry loop
                    }
                    catch (Exception ex)
                    {
                        if (IsAuthenticationError(ex)) throw; // bubble up to HandleBotFailureAsync
                        var rootMsg = GetRootMessage(ex);
                        if (attempt < maxAttempts)
                        {
                            await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Warning,
                                $"Step {step.StepNumber} error (attempt {attempt}/{maxAttempts}): {rootMsg} \u2014 retrying...");
                            continue;
                        }

                        step.Status = ActionItemStatus.Failed;
                        step.Error = rootMsg;
                        stepResults.Add($"Step {step.StepNumber} failed: {rootMsg}");

                        FireActionPlanChanged(bot.Id, plan);

                        DiagLog($"Bot {bot.Id} step {step.StepNumber} failed: {UnwrapException(ex)}");
                        await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Error,
                            $"Step {step.StepNumber} failed after {maxAttempts} attempts: {rootMsg}");
                    }
                }
            }
        }
        finally
        {
            await execClient.DisposeAsync();
        }

        // Synthesis with a fresh client (with retry)
        string? synthesisResult = null;
        if (stepResults.Count > 0 && !ct.IsCancellationRequested)
        {
            await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.StepStart, "Synthesizing results...");

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var synthClient = CreateBotSession(settings);
                try
                {
                    var synthRequest = new CreateInferenceRequest
                    {
                        ModelName = modelName,
                        InitializationPrompt = "You are a helpful assistant. Synthesize the results below into a clear summary.",
                        ThinkLevel = ThinkLevels.Basic
                    };
                    await synthClient.CreateAsync(synthRequest);

                    var synthSend = SendInferenceRequest.CreateDefault();
                    synthSend.Text = $"Goal: {bot.Goal}\n\nResults:\n{string.Join("\n", stepResults)}\n\nProvide a concise summary.";
                    synthSend.Temperature = 0.5f;
                    synthSend.MaxTokens = bot.MaxTokens;

                    var synthContent = new StringBuilder();
                    await foreach (var chunk in synthClient.SendAsync(synthSend, ct))
                    {
                        if (chunk.Type is InferenceResponseTypes.Text or InferenceResponseTypes.ToolContent)
                            synthContent.Append(chunk.Content);
                    }

                    var synthResult = synthContent.ToString().Trim();
                    if (settings.LogInferenceOutputEnabled)
                        await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Debug,
                            "Synthesis inference response", synthResult);

                    if (string.IsNullOrWhiteSpace(synthResult))
                    {
                        if (attempt < maxAttempts)
                        {
                            await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Warning,
                                $"Synthesis returned empty (attempt {attempt}/{maxAttempts}), possible session timeout \u2014 retrying...");
                            continue;
                        }
                        await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Error,
                            $"Synthesis returned empty after {maxAttempts} attempts");
                        break;
                    }

                    synthesisResult = synthResult;
                    await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Info,
                        "Summary: " + synthResult);
                    break; // success
                }
                catch (Exception ex)
                {
                    if (IsAuthenticationError(ex)) throw; // bubble up to HandleBotFailureAsync
                    var rootMsg = GetRootMessage(ex);
                    if (attempt < maxAttempts)
                    {
                        await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Warning,
                            $"Synthesis error (attempt {attempt}/{maxAttempts}): {rootMsg} \u2014 retrying...");
                        continue;
                    }
                    _logger.LogWarning(ex, "Synthesis failed for bot {BotId}", bot.Id);
                    await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Error,
                        $"Synthesis failed after {maxAttempts} attempts: {rootMsg}");
                }
                finally
                {
                    await synthClient.DisposeAsync();
                }
            }
        }

        return synthesisResult;
    }

    private async Task<string?> ExecuteDirectAsync(
        BotInstance bot, UserSettings settings,
        List<string> userInstructions, List<BotMemoryEntry> memories, CancellationToken ct)
    {
        const int maxAttempts = 3;
        var modelName = !string.IsNullOrWhiteSpace(bot.ModelName) ? bot.ModelName : settings.DefaultModelName;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var client = CreateBotSession(settings);
            try
            {
                var createRequest = new CreateInferenceRequest
                {
                    ModelName = modelName,
                    InitializationPrompt = BuildBotExecutionPrompt(bot, userInstructions, memories),
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
                await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Info, "Waiting for inference...");
                await foreach (var chunk in client.SendAsync(sendRequest, ct))
                {
                    if (chunk.Type == InferenceResponseTypes.Tooling)
                    {
                        await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.SkillAction, chunk.Content);
                    }
                    if (chunk.Type is InferenceResponseTypes.Text or InferenceResponseTypes.ToolContent)
                        content.Append(chunk.Content);
                }

                var directResult = content.ToString().Trim();
                if (settings.LogInferenceOutputEnabled)
                    await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Debug,
                        "Direct inference response", directResult);

                if (string.IsNullOrWhiteSpace(directResult))
                {
                    if (attempt < maxAttempts)
                    {
                        await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Warning,
                            $"Direct inference returned empty (attempt {attempt}/{maxAttempts}), possible session timeout \u2014 retrying...");
                        continue;
                    }
                    await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Error,
                        $"Direct inference returned empty after {maxAttempts} attempts");
                    return null;
                }

                await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Info,
                    "Result", directResult);
                return directResult;
            }
            catch (Exception ex)
            {
                if (IsAuthenticationError(ex)) throw; // bubble up to HandleBotFailureAsync
                var rootMsg = GetRootMessage(ex);
                if (attempt < maxAttempts)
                {
                    await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Warning,
                        $"Direct inference error (attempt {attempt}/{maxAttempts}): {rootMsg} \u2014 retrying...");
                    continue;
                }
                DiagLog($"Bot {bot.Id} direct inference failed: {UnwrapException(ex)}");
                await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Error,
                    $"Direct inference failed after {maxAttempts} attempts: {rootMsg}");
            }
            finally
            {
                await client.DisposeAsync();
            }
        }

        return null;
    }

    private async Task ExtractAndStoreMemoriesAsync(
        BotInstance bot, UserSettings settings, string cycleOutput, CancellationToken ct)
    {
        try
        {
            var modelName = !string.IsNullOrWhiteSpace(bot.ModelName) ? bot.ModelName : settings.DefaultModelName;
            var client = CreateBotSession(settings);
            try
            {
                var request = new CreateInferenceRequest
                {
                    ModelName = modelName,
                    InitializationPrompt = "You are a memory extraction assistant. Given a bot's execution output, extract key facts worth remembering for future runs.",
                    ThinkLevel = ThinkLevels.Basic
                };
                await client.CreateAsync(request);

                var sendRequest = SendInferenceRequest.CreateDefault();
                sendRequest.Text = $"""
                    Bot goal: {bot.Goal}
                    Execution #{bot.ExecutionCount} output:
                    {cycleOutput}

                    Extract the key facts worth remembering for future executions. Focus on:
                    - Specific outputs produced (so they are not repeated)
                    - Decisions made or approaches taken
                    - User preferences or feedback discovered
                    - Items to avoid repeating next time

                    Output one fact per line, no numbering, no bullets, no blank lines.
                    If there is nothing worth remembering, output exactly: NONE
                    """;
                sendRequest.Temperature = 0.2f;
                sendRequest.MaxTokens = 512;
                sendRequest.ThinkLevel = ThinkLevels.Basic;

                var content = new StringBuilder();
                await foreach (var chunk in client.SendAsync(sendRequest, ct))
                {
                    if (chunk.Type is InferenceResponseTypes.Text or InferenceResponseTypes.ToolContent)
                        content.Append(chunk.Content);
                }

                var raw = content.ToString().Trim();
                if (string.IsNullOrWhiteSpace(raw) || raw.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                    return;

                var facts = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var fact in facts)
                {
                    if (string.IsNullOrWhiteSpace(fact) || fact.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                        continue;

                    await _botStore.AddMemoryAsync(new BotMemoryEntry
                    {
                        BotId = bot.Id,
                        ExecutionNumber = bot.ExecutionCount,
                        Content = fact
                    });
                }

                await _botStore.PruneMemoryAsync(bot.Id, bot.MaxMemoryEntries);

                await LogAsync(bot.Id, bot.ExecutionCount, BotLogLevel.Info,
                    $"Memory updated ({facts.Length} fact{(facts.Length == 1 ? "" : "s")} extracted)");
            }
            finally
            {
                await client.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memory extraction failed for bot {BotId} — this is non-fatal", bot.Id);
        }
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

    private async Task HandleBotFailureAsync(Guid botId, Exception ex)
    {
        var bot = await _botStore.GetAsync(botId);
        if (bot is null) return;

        var rootMessage = GetRootMessage(ex);

        // Log full exception chain for diagnostics
        DiagLog($"Bot {botId} failed: {UnwrapException(ex)}");

        // If this is a 401/Unauthenticated error, stop the bot and notify the user to re-login
        if (IsAuthenticationError(ex))
        {
            bot.LastError = "Authentication failed — the ORC reports your access is unauthorized. Please log in again (F5).";
            bot.Status = BotStatus.Stopped;
            bot.NextRunAt = null;
            await _botStore.UpdateAsync(bot);

            await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Error,
                "Authentication failed — the ORC reports your access is unauthorized. Please log in again (F5).");

            BotStatusChanged?.Invoke(this, bot);
            AuthenticationRequired?.Invoke(this, botId);
            return;
        }

        bot.LastError = rootMessage;
        bot.RetryGuidance = $"Previous attempt failed: {rootMessage}";
        ComputeRetryRun(bot);
        await _botStore.UpdateAsync(bot);

        await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Error, $"Execution failed: {rootMessage}");

        await LogAsync(botId, bot.ExecutionCount, BotLogLevel.Info,
            "Send a message to provide guidance for the next attempt, or use /stop to stop the bot.");

        BotStatusChanged?.Invoke(this, bot);
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

            if (runtime.LogFileWriter is { } writer)
            {
                try
                {
                    var time = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss");
                    writer.WriteLine($"[{time}] [{entry.Level,-13}] {entry.Message}");
                    if (!string.IsNullOrWhiteSpace(entry.Detail))
                    {
                        foreach (var line in entry.Detail.Split('\n'))
                            writer.WriteLine($"{"",28}{line.TrimEnd('\r')}");
                    }
                    writer.Flush();
                }
                catch { /* don't let file I/O break execution */ }
            }
        }

        BotLogEntryAdded?.Invoke(this, entry);
    }

    private void FireActionPlanChanged(Guid botId, ActionPlan plan)
    {
        ActionPlanChanged?.Invoke(this, new ActionPlanChangedEventArgs { BotId = botId, Plan = plan });
    }

    private BotInferenceSession CreateBotSession(UserSettings settings)
    {
        if (settings.HostModeEnabled && _localInference is not null)
            return BotInferenceSession.CreateLocal(_localInference);

        var sessionClientFactory = new SessionClientFactory(_keyProvider);
        var sessionManager = new InferenceSessionManager(
            sessionClientFactory,
            _keyProvider,
            _loggerFactory.CreateLogger<InferenceSessionManager>());
        var factory = new InferenceClientFactory(sessionManager);
        return BotInferenceSession.CreateOrc(factory.Create());
    }

    /// <summary>
    /// Abstracts ORC-based InferenceClient and local ILocalInferenceService
    /// behind a unified Create/Send/Dispose pattern.
    /// </summary>
    private sealed class BotInferenceSession : IAsyncDisposable
    {
        private readonly InferenceClient? _orcClient;
        private readonly ILocalInferenceService? _local;
        private string? _inferenceId;
        private string? _sessionId;

        public static BotInferenceSession CreateOrc(InferenceClient client) => new(client, null);
        public static BotInferenceSession CreateLocal(ILocalInferenceService local) => new(null, local);

        private BotInferenceSession(InferenceClient? orc, ILocalInferenceService? local)
        {
            _orcClient = orc;
            _local = local;
        }

        public async Task CreateAsync(CreateInferenceRequest request)
        {
            if (_local is not null)
            {
                var response = await _local.CreateSessionAsync(request);
                _inferenceId = response.InferenceId;
                _sessionId = response.SessionId;
            }
            else
            {
                await _orcClient!.CreateAsync(request);
            }
        }

        public async IAsyncEnumerable<SendInferenceResponse> SendAsync(
            SendInferenceRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (_local is not null)
            {
                request.InferenceId = _inferenceId!;
                request.SessionId = _sessionId!;
                await foreach (var response in _local.SendAsync(request, ct))
                    yield return response;
            }
            else
            {
                var stream = _orcClient!.Send(request);
                while (await stream.ResponseStream.MoveNext(ct))
                    yield return stream.ResponseStream.Current;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_local is not null && _inferenceId is not null)
                try { await _local.CloseSessionAsync(_inferenceId); } catch { }
            else if (_orcClient is not null)
                try { await _orcClient.CloseAsync(); } catch { }
        }
    }

    private static string BuildBotPlanningPrompt(BotInstance bot, List<string>? userInstructions = null, List<BotMemoryEntry>? memories = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an autonomous bot task planner.");
        if (!string.IsNullOrWhiteSpace(bot.Persona))
            sb.AppendLine($"Persona: {bot.Persona}");
        sb.AppendLine();

        if (memories is { Count: > 0 })
        {
            sb.AppendLine("MEMORY — Important context from previous executions (do NOT repeat previous outputs):");
            foreach (var m in memories)
                sb.AppendLine($"- [Run #{m.ExecutionNumber}] {m.Content}");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(bot.RetryGuidance))
        {
            sb.AppendLine("IMPORTANT — This is a retry after a previous failure. Adjust your plan accordingly:");
            sb.AppendLine(bot.RetryGuidance);
            sb.AppendLine();
        }

        if (userInstructions is { Count: > 0 })
        {
            sb.AppendLine("The user has provided the following instructions for this execution:");
            foreach (var instruction in userInstructions)
                sb.AppendLine($"- {instruction}");
            sb.AppendLine();
        }

        sb.AppendLine("""
            Given a goal, break it down into simple steps.

            Rules:
            - Each step must be a single, specific action
            - Use 2 to 5 steps maximum
            - Do NOT include a "summarize" step - that happens automatically
            - You MUST respond with ONLY the XML below — no other text, no commentary, no markdown

            You must use this exact XML format:
            <plan>
            <goal>One sentence describing the goal</goal>
            <step>First thing to do</step>
            <step>Second thing to do</step>
            </plan>

            Example — if the goal is "Get the weather forecast":
            <plan>
            <goal>Get the weather forecast for today</goal>
            <step>Search for the current weather conditions</step>
            <step>Look up the forecast for the rest of the day</step>
            <step>Format the weather information clearly</step>
            </plan>
            """);
        return sb.ToString();
    }

    private static string BuildBotExecutionPrompt(BotInstance bot, List<string>? userInstructions = null, List<BotMemoryEntry>? memories = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an autonomous bot executing a task.");
        if (!string.IsNullOrWhiteSpace(bot.Persona))
            sb.AppendLine($"Persona: {bot.Persona}");
        sb.AppendLine($"Goal: {bot.Goal}");

        if (memories is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("MEMORY — Important context from previous executions (do NOT repeat previous outputs):");
            foreach (var m in memories)
                sb.AppendLine($"- [Run #{m.ExecutionNumber}] {m.Content}");
        }

        if (!string.IsNullOrWhiteSpace(bot.RetryGuidance))
        {
            sb.AppendLine();
            sb.AppendLine("IMPORTANT — This is a retry after a previous failure. Take the following into account:");
            sb.AppendLine(bot.RetryGuidance);
        }

        if (userInstructions is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("The user has provided the following instructions for this execution:");
            foreach (var instruction in userInstructions)
                sb.AppendLine($"- {instruction}");
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

    private static void WriteLogFileHeader(StreamWriter writer, BotInstance bot, UserSettings settings)
    {
        var divider = new string('\u2550', 60);
        writer.WriteLine(divider);
        writer.WriteLine("  Bot Run Log");
        writer.WriteLine(divider);
        writer.WriteLine($"  Bot:          {bot.Label}");
        writer.WriteLine($"  Goal:         {bot.Goal}");
        writer.WriteLine($"  Persona:      {bot.Persona ?? "(none)"}");
        writer.WriteLine($"  Model:        {(!string.IsNullOrWhiteSpace(bot.ModelName) ? bot.ModelName : settings.DefaultModelName)}");
        writer.WriteLine($"  Temperature:  {bot.Temperature:F1}");
        writer.WriteLine($"  Max Tokens:   {bot.MaxTokens}");
        writer.WriteLine($"  Schedule:     {bot.ScheduleType}{(bot.ScheduleIntervalMinutes > 0 ? $" (every {bot.ScheduleIntervalMinutes} min)" : "")}");

        var skillIds = bot.GetEnabledSkillIds();
        writer.WriteLine($"  Skills:       {(skillIds.Count > 0 ? string.Join(", ", skillIds) : "(all)")}");

        var toolGroups = settings.GetEnabledToolGroups();
        writer.WriteLine($"  Tool Groups:  {(toolGroups.Count > 0 ? string.Join(", ", toolGroups) : "(none)")}");

        writer.WriteLine($"  Run #:        {bot.ExecutionCount}");
        writer.WriteLine($"  Started:      {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine(divider);
        writer.WriteLine();
        writer.Flush();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        // Replace spaces with hyphens for readability
        return sb.ToString().Replace(' ', '-').Trim('-');
    }

    public void Dispose()
    {
        _schedulerTimer.Dispose();
        foreach (var kvp in _runtimes)
        {
            kvp.Value.LogFileWriter?.Dispose();
            kvp.Value.Cts.Cancel();
            kvp.Value.Cts.Dispose();
        }
        _runtimes.Clear();
    }

    /// <summary>
    /// Unwrap TargetInvocationException and AggregateException to get the real error message.
    /// </summary>
    private static string GetRootMessage(Exception ex)
    {
        var inner = ex;
        while (inner is System.Reflection.TargetInvocationException or AggregateException && inner.InnerException is not null)
            inner = inner.InnerException;
        return inner.Message;
    }

    /// <summary>
    /// Check if the exception (or any inner exception) is a gRPC Unauthenticated (401) error.
    /// </summary>
    private static bool IsAuthenticationError(Exception ex)
    {
        var current = ex;
        while (current is not null)
        {
            if (current is RpcException rpcEx && rpcEx.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
                return true;
            current = current.InnerException;
        }
        return false;
    }

    private static readonly string _diagLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DaisiHost", "tui-diag.log");

    private static void DiagLog(string message)
    {
        try
        {
            File.AppendAllText(_diagLogPath,
                $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private static string UnwrapException(Exception ex)
    {
        var msg = $"{ex.GetType().Name}: {ex.Message}\n  {ex.StackTrace}";
        var inner = ex.InnerException;
        while (inner is not null)
        {
            msg += $"\n  --- Inner: {inner.GetType().Name}: {inner.Message}\n  {inner.StackTrace}";
            inner = inner.InnerException;
        }
        return msg;
    }

    private class BotRuntime(CancellationTokenSource cts, Channel<BotLogEntry> outputChannel)
    {
        public CancellationTokenSource Cts { get; } = cts;
        public Channel<BotLogEntry> OutputChannel { get; } = outputChannel;
        public Task? ExecutionTask { get; set; }
        public ConcurrentQueue<string> UserMessages { get; } = new();
        public StreamWriter? LogFileWriter { get; set; }
    }
}
