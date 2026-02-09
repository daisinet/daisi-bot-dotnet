using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Dialogs;

/// <summary>
/// Model selection dialog: shows available models in a list, think level selection, save to settings.
/// </summary>
public class ModelPickerFlow : IModal
{
    private readonly App _app;
    private readonly IModelService _modelService;
    private readonly ISettingsService _settingsService;
    private DialogRunner.BoxBounds? _box;

    private List<AvailableModel> _models = [];
    private int _selectedModel;
    private int _selectedThinkLevel;
    private string _statusText = "Loading models...";
    private ConsoleColor _statusColor = ConsoleColor.Yellow;
    private bool _loaded;

    private enum FocusArea { ModelList, ThinkLevel }
    private FocusArea _focus = FocusArea.ModelList;

    private static readonly string[] ThinkLevelLabels = ["Basic", "Basic + Tools", "Chain of Thought", "Tree of Thought"];

    public ModelPickerFlow(App app, IServiceProvider services)
    {
        _app = app;
        _modelService = services.GetRequiredService<IModelService>();
        _settingsService = services.GetRequiredService<ISettingsService>();
        LoadModels();
    }

    private void LoadModels()
    {
        Task.Run(async () =>
        {
            try
            {
                var models = await _modelService.GetAvailableModelsAsync();
                var settings = await _settingsService.GetSettingsAsync();
                _app.Post(() =>
                {
                    _models = models;
                    _loaded = true;

                    // Select current model
                    var idx = _models.FindIndex(m => m.Name == settings.DefaultModelName);
                    if (idx >= 0) _selectedModel = idx;

                    _selectedThinkLevel = (int)settings.DefaultThinkLevel;
                    _statusText = $"{_models.Count} models available";
                    _statusColor = ConsoleColor.Gray;
                    Draw();
                    AnsiConsole.Flush();
                });
            }
            catch (Exception ex)
            {
                _app.Post(() =>
                {
                    _statusText = $"Error: {ex.Message}";
                    _statusColor = ConsoleColor.Red;
                    _loaded = true;
                    Draw();
                    AnsiConsole.Flush();
                });
            }
        });
    }

    public void Draw()
    {
        var boxWidth = 55;
        var boxHeight = 20;
        _box = DialogRunner.DrawCenteredBox(_app, "Select Model", boxWidth, boxHeight);

        // Model list area
        DialogRunner.DrawLabel(_box, 0, _focus == FocusArea.ModelList
            ? "> Available Models:"
            : "  Available Models:");

        var listHeight = 8;
        for (var i = 0; i < listHeight; i++)
        {
            var row = _box.InnerTop + 1 + i;
            if (i < _models.Count)
            {
                var m = _models[i];
                var flags = new List<string>();
                if (m.IsDefault) flags.Add("default");
                if (m.IsMultiModal) flags.Add("multi-modal");
                if (m.HasReasoning) flags.Add("reasoning");
                var suffix = flags.Count > 0 ? $" ({string.Join(", ", flags)})" : "";
                var text = $"  {m.Name}{suffix}";
                if (text.Length > _box.InnerWidth) text = text[..(_box.InnerWidth - 2)] + "..";

                if (i == _selectedModel && _focus == FocusArea.ModelList)
                    AnsiConsole.WriteAtReverse(row, _box.InnerLeft, text, _box.InnerWidth);
                else if (i == _selectedModel)
                {
                    AnsiConsole.SetForeground(ConsoleColor.Cyan);
                    AnsiConsole.WriteAt(row, _box.InnerLeft, text.PadRight(_box.InnerWidth), _box.InnerWidth);
                    AnsiConsole.ResetStyle();
                }
                else
                    AnsiConsole.WriteAt(row, _box.InnerLeft, text.PadRight(_box.InnerWidth), _box.InnerWidth);
            }
            else
            {
                AnsiConsole.WriteAt(row, _box.InnerLeft, new string(' ', _box.InnerWidth));
            }
        }

        // Think level
        var thinkRow = _box.InnerTop + 10;
        AnsiConsole.WriteAt(thinkRow, _box.InnerLeft,
            (_focus == FocusArea.ThinkLevel ? "> " : "  ") + "Think Level:");

        for (var i = 0; i < ThinkLevelLabels.Length; i++)
        {
            var row = thinkRow + 1 + i;
            var marker = i == _selectedThinkLevel ? "(o) " : "( ) ";
            var text = $"  {marker}{ThinkLevelLabels[i]}";

            if (i == _selectedThinkLevel && _focus == FocusArea.ThinkLevel)
                AnsiConsole.WriteAtReverse(row, _box.InnerLeft, text, _box.InnerWidth);
            else
                AnsiConsole.WriteAt(row, _box.InnerLeft, text.PadRight(_box.InnerWidth), _box.InnerWidth);
        }

        // Status & hints
        DialogRunner.DrawStatus(_box, _statusText, _statusColor);
        DialogRunner.DrawButtonHints(_box, " Tab:Switch  Enter:Save  Esc:Cancel ");
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        if (!_loaded) return;

        switch (key.Key)
        {
            case ConsoleKey.Escape:
                _app.CloseModal();
                break;

            case ConsoleKey.Tab:
                _focus = _focus == FocusArea.ModelList ? FocusArea.ThinkLevel : FocusArea.ModelList;
                Draw();
                break;

            case ConsoleKey.UpArrow:
                if (_focus == FocusArea.ModelList && _selectedModel > 0)
                    _selectedModel--;
                else if (_focus == FocusArea.ThinkLevel && _selectedThinkLevel > 0)
                    _selectedThinkLevel--;
                Draw();
                break;

            case ConsoleKey.DownArrow:
                if (_focus == FocusArea.ModelList && _selectedModel < _models.Count - 1)
                    _selectedModel++;
                else if (_focus == FocusArea.ThinkLevel && _selectedThinkLevel < ThinkLevelLabels.Length - 1)
                    _selectedThinkLevel++;
                Draw();
                break;

            case ConsoleKey.Enter:
                SaveAndClose();
                break;
        }
    }

    private void SaveAndClose()
    {
        if (_selectedModel < 0 || _selectedModel >= _models.Count) return;

        var selected = _models[_selectedModel];
        var thinkLevel = (ConversationThinkLevel)_selectedThinkLevel;

        Task.Run(async () =>
        {
            var settings = await _settingsService.GetSettingsAsync();
            settings.DefaultModelName = selected.Name;
            settings.DefaultThinkLevel = thinkLevel;
            await _settingsService.SaveSettingsAsync(settings);
            _app.Post(() => _app.CloseModal());
        });
    }
}
