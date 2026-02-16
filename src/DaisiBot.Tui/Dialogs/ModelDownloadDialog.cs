using System.Diagnostics;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Dialogs;

/// <summary>
/// Modal dialog that downloads required models at startup, showing progress.
/// Starts with a confirmation prompt before downloading.
/// </summary>
public class ModelDownloadDialog : IModal
{
    private enum State { Checking, Confirming, Downloading, Done, Error, Cancelled }

    private readonly App _app;
    private readonly ILocalInferenceService _localInference;
    private DialogRunner.BoxBounds? _box;

    private List<ModelDownloadInfo> _models = [];
    private int _currentIndex;
    private string _currentModelName = "";
    private double _progress;
    private long _bytesDownloaded;
    private long? _totalBytes;
    private double _speedMBps;
    private long _lastSpeedBytes;
    private long _lastSpeedTimestamp = Stopwatch.GetTimestamp();
    private string? _error;
    private State _state = State.Checking;
    private bool _started;
    private CancellationTokenSource? _cts;

    public ModelDownloadDialog(App app, IServiceProvider services)
    {
        _app = app;
        _localInference = services.GetRequiredService<ILocalInferenceService>();
    }

    public void Draw()
    {
        _box = DialogRunner.DrawCenteredBox(_app, "Downloading Models", 44, 12);

        switch (_state)
        {
            case State.Checking:
                DialogRunner.DrawLabel(_box, 1, "Checking required models...");
                break;

            case State.Confirming:
                DrawConfirming();
                break;

            case State.Downloading:
                DrawDownloading();
                break;

            case State.Done:
                DialogRunner.DrawLabel(_box, 2, "Models ready!");
                DialogRunner.DrawButtonHints(_box, " Esc:Close ");
                break;

            case State.Error:
                DialogRunner.DrawLabel(_box, 1, "Download failed:");
                var errDisplay = _error!.Length > _box.InnerWidth ? _error[.._box.InnerWidth] : _error;
                AnsiConsole.SetForeground(ConsoleColor.Red);
                DialogRunner.DrawLabel(_box, 3, errDisplay);
                AnsiConsole.ResetStyle();
                DialogRunner.DrawButtonHints(_box, " Esc:Close ");
                break;

            case State.Cancelled:
                DialogRunner.DrawLabel(_box, 2, "Download cancelled.");
                DialogRunner.DrawButtonHints(_box, " Esc:Close ");
                break;
        }

        // Kick off the initial check on first draw
        if (!_started)
        {
            _started = true;
            CheckModels();
        }
    }

    private void DrawConfirming()
    {
        var line = $"{_models.Count} model(s) need to be downloaded:";
        DialogRunner.DrawLabel(_box!, 1, line);

        // List model names (up to what fits in the box)
        var maxRows = _box!.InnerHeight - 4; // leave room for header + hint rows
        for (var i = 0; i < _models.Count && i < maxRows; i++)
        {
            var name = _models[i].Name;
            if (name.Length > _box.InnerWidth - 2)
                name = name[..(_box.InnerWidth - 2)];
            DialogRunner.DrawLabel(_box, 2 + i, $"  {name}");
        }

        if (_models.Count > maxRows)
            DialogRunner.DrawLabel(_box, 2 + maxRows, $"  ...and {_models.Count - maxRows} more");

        DialogRunner.DrawButtonHints(_box, " Enter:Download  Esc:Skip ");
    }

    private void DrawDownloading()
    {
        // Model name and index
        var header = $"{_currentModelName} ({_currentIndex + 1} of {_models.Count})";
        if (header.Length > _box!.InnerWidth)
            header = header[.._box.InnerWidth];
        DialogRunner.DrawLabel(_box, 1, header);

        // Progress bar
        var barWidth = _box.InnerWidth;
        var filledCount = (int)(_progress / 100.0 * barWidth);
        if (filledCount > barWidth) filledCount = barWidth;
        var bar = new string('\u2588', filledCount) + new string('\u2591', barWidth - filledCount);
        DialogRunner.DrawLabel(_box, 3, bar);

        // Percentage + bytes + speed
        string pctLine;
        var speedStr = _speedMBps > 0 ? $"  {_speedMBps:F1} MB/s" : "";
        if (_totalBytes.HasValue && _totalBytes.Value > 0)
        {
            var dlMb = _bytesDownloaded / (1024.0 * 1024.0);
            var totalMb = _totalBytes.Value / (1024.0 * 1024.0);
            pctLine = $"{_progress:F1}% \u2014 {dlMb:N0} / {totalMb:N0} MB{speedStr}";
        }
        else
        {
            var dlMb = _bytesDownloaded / (1024.0 * 1024.0);
            pctLine = $"{dlMb:N0} MB downloaded{speedStr}";
        }
        DialogRunner.DrawLabel(_box, 5, pctLine.PadRight(_box.InnerWidth));

        DialogRunner.DrawButtonHints(_box, " Esc:Cancel ");
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            switch (_state)
            {
                case State.Confirming:
                    // User declined — close without downloading
                    _app.CloseModal();
                    break;

                case State.Downloading:
                    // Cancel in-flight download
                    _cts?.Cancel();
                    break;

                case State.Done:
                    _app.CloseModal();
                    // Initialize inference after successful download
                    Task.Run(async () =>
                    {
                        try { await _localInference.InitializeAsync(); }
                        catch { /* logged internally */ }
                    });
                    break;

                case State.Error:
                case State.Cancelled:
                    _app.CloseModal();
                    break;
            }
        }
        else if (key.Key == ConsoleKey.Enter && _state == State.Confirming)
        {
            StartDownload();
        }
    }

    private void CheckModels()
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
                    _app.Post(() => _app.CloseModal());
                    return;
                }

                _app.Post(() =>
                {
                    _models = models;
                    _state = State.Confirming;
                    Draw();
                    AnsiConsole.Flush();
                });
            }
            catch (Exception ex)
            {
                _app.Post(() =>
                {
                    _error = ex.Message;
                    _state = State.Error;
                    Draw();
                    AnsiConsole.Flush();
                });
            }
        });
    }

    private void StartDownload()
    {
        _state = State.Downloading;
        _currentIndex = 0;
        _currentModelName = _models[0].Name;
        _progress = 0;
        _bytesDownloaded = 0;
        _totalBytes = null;
        _cts = new CancellationTokenSource();

        Draw();
        AnsiConsole.Flush();

        var ct = _cts.Token;

        Task.Run(async () =>
        {
            try
            {
                for (var i = 0; i < _models.Count; i++)
                {
                    var idx = i;
                    var model = _models[idx];

                    _app.Post(() =>
                    {
                        _currentIndex = idx;
                        _currentModelName = model.Name;
                        _progress = 0;
                        _bytesDownloaded = 0;
                        _totalBytes = null;
                        _speedMBps = 0;
                        _lastSpeedBytes = 0;
                        _lastSpeedTimestamp = Stopwatch.GetTimestamp();
                        Draw();
                        AnsiConsole.Flush();
                    });

                    await _localInference.DownloadModelAsync(model, (pct, downloaded, total) =>
                    {
                        _app.Post(() =>
                        {
                            // Calculate download speed
                            var now = Stopwatch.GetTimestamp();
                            var elapsed = Stopwatch.GetElapsedTime(_lastSpeedTimestamp, now).TotalSeconds;
                            if (elapsed > 0)
                            {
                                _speedMBps = (downloaded - _lastSpeedBytes) / (1024.0 * 1024.0) / elapsed;
                                _lastSpeedBytes = downloaded;
                                _lastSpeedTimestamp = now;
                            }

                            _progress = pct;
                            _bytesDownloaded = downloaded;
                            _totalBytes = total;
                            Draw();
                            AnsiConsole.Flush();
                        });
                    }, ct);
                }

                // All downloads complete — initialize
                await _localInference.InitializeAsync();

                _app.Post(() =>
                {
                    _state = State.Done;
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
            catch (OperationCanceledException)
            {
                _app.Post(() =>
                {
                    _state = State.Cancelled;
                    Draw();
                    AnsiConsole.Flush();
                });
            }
            catch (Exception ex)
            {
                _app.Post(() =>
                {
                    _error = ex.Message;
                    _state = State.Error;
                    Draw();
                    AnsiConsole.Flush();
                });
            }
        });
    }
}
