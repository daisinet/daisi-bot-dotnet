using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Dialogs;

/// <summary>
/// Modal dialog that downloads required models at startup, showing progress.
/// </summary>
public class ModelDownloadDialog : IModal
{
    private readonly App _app;
    private readonly ILocalInferenceService _localInference;
    private DialogRunner.BoxBounds? _box;

    private List<ModelDownloadInfo> _models = [];
    private int _currentIndex;
    private string _currentModelName = "";
    private double _progress;
    private string _progressText = "";
    private string? _error;
    private bool _done;
    private bool _started;

    public ModelDownloadDialog(App app, IServiceProvider services)
    {
        _app = app;
        _localInference = services.GetRequiredService<ILocalInferenceService>();
    }

    public void Draw()
    {
        _box = DialogRunner.DrawCenteredBox(_app, "Downloading Models", 44, 10);

        if (_models.Count == 0 && !_done && _error is null)
        {
            DialogRunner.DrawLabel(_box, 1, "Checking required models...");
        }
        else if (_error is not null)
        {
            DialogRunner.DrawLabel(_box, 1, "Download failed:");
            var errDisplay = _error.Length > _box.InnerWidth ? _error[.._box.InnerWidth] : _error;
            AnsiConsole.SetForeground(ConsoleColor.Red);
            DialogRunner.DrawLabel(_box, 3, errDisplay);
            AnsiConsole.ResetStyle();
        }
        else if (_done)
        {
            DialogRunner.DrawLabel(_box, 2, "Models ready!");
        }
        else
        {
            // Model name and index
            var header = $"{_currentModelName} ({_currentIndex + 1} of {_models.Count})";
            if (header.Length > _box.InnerWidth)
                header = header[.._box.InnerWidth];
            DialogRunner.DrawLabel(_box, 1, header);

            // Progress bar
            var barWidth = _box.InnerWidth;
            var filledCount = (int)(_progress / 100.0 * barWidth);
            if (filledCount > barWidth) filledCount = barWidth;
            var bar = new string('\u2588', filledCount) + new string('\u2591', barWidth - filledCount);
            DialogRunner.DrawLabel(_box, 3, bar);

            // Percentage
            var pctText = $"{_progress:F1}%";
            DialogRunner.DrawLabel(_box, 4, pctText.PadRight(_box.InnerWidth));

            // Size text
            if (_progressText.Length > 0)
                DialogRunner.DrawLabel(_box, 5, _progressText.PadRight(_box.InnerWidth));
        }

        // Hints
        var hints = _done || _error is not null ? " Esc:Close " : " Esc:Skip ";
        DialogRunner.DrawButtonHints(_box, hints);

        // Start download on first draw
        if (!_started)
        {
            _started = true;
            StartDownload();
        }
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            _app.CloseModal();

            // If downloads completed successfully, initialize inference
            if (_done)
            {
                Task.Run(async () =>
                {
                    try { await _localInference.InitializeAsync(); }
                    catch { /* logged internally */ }
                });
            }
        }
    }

    private void StartDownload()
    {
        Task.Run(async () =>
        {
            try
            {
                var models = await _localInference.GetRequiredDownloadsAsync();

                if (models.Count == 0)
                {
                    // All models present — just initialize and close
                    await _localInference.InitializeAsync();
                    _app.Post(() =>
                    {
                        _app.CloseModal();
                    });
                    return;
                }

                _app.Post(() =>
                {
                    _models = models;
                    _currentIndex = 0;
                    _currentModelName = models[0].Name;
                    Draw();
                    AnsiConsole.Flush();
                });

                for (var i = 0; i < models.Count; i++)
                {
                    var idx = i;
                    var model = models[idx];

                    _app.Post(() =>
                    {
                        _currentIndex = idx;
                        _currentModelName = model.Name;
                        _progress = 0;
                        _progressText = "";
                        Draw();
                        AnsiConsole.Flush();
                    });

                    await _localInference.DownloadModelAsync(model, progress =>
                    {
                        _app.Post(() =>
                        {
                            _progress = progress;
                            _progressText = $"{progress:F1}% complete";
                            Draw();
                            AnsiConsole.Flush();
                        });
                    });
                }

                // All downloads complete — initialize
                await _localInference.InitializeAsync();

                _app.Post(() =>
                {
                    _done = true;
                    Draw();
                    AnsiConsole.Flush();

                    // Auto-close after a brief moment
                    Task.Run(async () =>
                    {
                        await Task.Delay(800);
                        _app.Post(() => _app.CloseModal());
                    });
                });
            }
            catch (Exception ex)
            {
                _app.Post(() =>
                {
                    _error = ex.Message;
                    Draw();
                    AnsiConsole.Flush();
                });
            }
        });
    }
}
