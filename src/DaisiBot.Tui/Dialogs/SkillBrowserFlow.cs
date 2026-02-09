using System.Text;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models.Skills;
using DaisiBot.Core.Security;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Dialogs;

/// <summary>
/// Skill browser: search field + scrollable skill list + detail view.
/// </summary>
public class SkillBrowserFlow : IModal
{
    private readonly App _app;
    private readonly IServiceProvider _services;
    private DialogRunner.BoxBounds? _box;

    private List<Skill> _skills = [];
    private int _selectedSkill;
    private readonly StringBuilder _searchBuffer = new();
    private int _searchCursor;
    private string _statusText = "Loading skills...";
    private ConsoleColor _statusColor = ConsoleColor.Yellow;

    private enum FocusArea { Search, List, Detail }
    private FocusArea _focus = FocusArea.Search;

    private int _detailScroll;
    private List<string> _detailLines = [];

    public SkillBrowserFlow(App app, IServiceProvider services)
    {
        _app = app;
        _services = services;
        SearchSkills();
    }

    private void SearchSkills()
    {
        var query = _searchBuffer.ToString().Trim();
        var skillService = _services.GetService<ISkillService>();

        if (skillService is null)
        {
            _statusText = "Skill service not available";
            _statusColor = ConsoleColor.Red;
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                var skills = await skillService.GetPublicSkillsAsync(
                    string.IsNullOrWhiteSpace(query) ? null : query);
                _app.Post(() =>
                {
                    _skills = skills;
                    _selectedSkill = 0;
                    _statusText = $"{skills.Count} skills found";
                    _statusColor = ConsoleColor.Gray;
                    UpdateDetailView();
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
                    Draw();
                    AnsiConsole.Flush();
                });
            }
        });
    }

    public void Draw()
    {
        var boxWidth = Math.Min(_app.Width - 4, 80);
        var boxHeight = Math.Min(_app.Height - 4, 24);
        _box = DialogRunner.DrawCenteredBox(_app, "Skill Browser", boxWidth, boxHeight);

        // Search field
        AnsiConsole.WriteAt(_box.InnerTop, _box.InnerLeft, "Search: ");
        var searchFieldLeft = _box.InnerLeft + 8;
        var searchFieldWidth = _box.InnerWidth / 2 - 8;
        if (searchFieldWidth < 10) searchFieldWidth = 10;
        var searchDisplay = _searchBuffer.ToString().PadRight(searchFieldWidth);
        if (searchDisplay.Length > searchFieldWidth) searchDisplay = searchDisplay[..searchFieldWidth];

        if (_focus == FocusArea.Search)
            AnsiConsole.SetReverse();
        AnsiConsole.WriteAt(_box.InnerTop, searchFieldLeft, searchDisplay);
        AnsiConsole.ResetStyle();

        // Separator
        var listWidth = _box.InnerWidth / 2;
        var detailLeft = _box.InnerLeft + listWidth + 1;
        var detailWidth = _box.InnerWidth - listWidth - 1;

        // Vertical separator
        for (var r = _box.InnerTop + 1; r < _box.InnerTop + _box.InnerHeight - 1; r++)
            AnsiConsole.WriteAt(r, _box.InnerLeft + listWidth, "\u2502");

        // Skill list
        var listTop = _box.InnerTop + 2;
        var listHeight = _box.InnerHeight - 4;

        for (var i = 0; i < listHeight; i++)
        {
            var row = listTop + i;
            if (i < _skills.Count)
            {
                var s = _skills[i];
                var text = $"{s.Name} v{s.Version}";
                if (text.Length > listWidth - 1)
                    text = text[..(listWidth - 3)] + "..";

                if (i == _selectedSkill && _focus == FocusArea.List)
                    AnsiConsole.WriteAtReverse(row, _box.InnerLeft, text, listWidth);
                else if (i == _selectedSkill)
                {
                    AnsiConsole.SetForeground(ConsoleColor.Cyan);
                    AnsiConsole.WriteAt(row, _box.InnerLeft, text.PadRight(listWidth), listWidth);
                    AnsiConsole.ResetStyle();
                }
                else
                    AnsiConsole.WriteAt(row, _box.InnerLeft, text.PadRight(listWidth), listWidth);
            }
            else
            {
                AnsiConsole.WriteAt(row, _box.InnerLeft, new string(' ', listWidth));
            }
        }

        // Detail view
        for (var i = 0; i < listHeight; i++)
        {
            var row = listTop + i;
            var lineIdx = _detailScroll + i;
            if (lineIdx < _detailLines.Count)
            {
                var line = _detailLines[lineIdx];
                if (line.Length > detailWidth) line = line[..detailWidth];
                AnsiConsole.WriteAt(row, detailLeft, line.PadRight(detailWidth));
            }
            else
            {
                AnsiConsole.WriteAt(row, detailLeft, new string(' ', detailWidth));
            }
        }

        // Status
        DialogRunner.DrawStatus(_box, _statusText, _statusColor);
        DialogRunner.DrawButtonHints(_box, " Tab:Focus  Enter:Search  Esc:Close ");

        // Cursor
        if (_focus == FocusArea.Search)
        {
            AnsiConsole.ShowCursor();
            AnsiConsole.MoveTo(_box.InnerTop, searchFieldLeft + _searchCursor);
        }
        else
        {
            AnsiConsole.HideCursor();
        }
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape:
                AnsiConsole.HideCursor();
                _app.CloseModal();
                return;

            case ConsoleKey.Tab:
                _focus = _focus switch
                {
                    FocusArea.Search => FocusArea.List,
                    FocusArea.List => FocusArea.Detail,
                    _ => FocusArea.Search
                };
                Draw();
                return;
        }

        switch (_focus)
        {
            case FocusArea.Search:
                HandleSearchInput(key);
                break;
            case FocusArea.List:
                HandleListInput(key);
                break;
            case FocusArea.Detail:
                HandleDetailInput(key);
                break;
        }
    }

    private void HandleSearchInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                SearchSkills();
                break;
            case ConsoleKey.Backspace:
                if (_searchCursor > 0) { _searchBuffer.Remove(_searchCursor - 1, 1); _searchCursor--; }
                Draw();
                break;
            case ConsoleKey.Delete:
                if (_searchCursor < _searchBuffer.Length) _searchBuffer.Remove(_searchCursor, 1);
                Draw();
                break;
            case ConsoleKey.LeftArrow:
                if (_searchCursor > 0) _searchCursor--;
                Draw();
                break;
            case ConsoleKey.RightArrow:
                if (_searchCursor < _searchBuffer.Length) _searchCursor++;
                Draw();
                break;
            case ConsoleKey.Home:
                _searchCursor = 0;
                Draw();
                break;
            case ConsoleKey.End:
                _searchCursor = _searchBuffer.Length;
                Draw();
                break;
            default:
                if (key.KeyChar >= 32 && key.KeyChar < 127)
                {
                    _searchBuffer.Insert(_searchCursor, key.KeyChar);
                    _searchCursor++;
                    Draw();
                }
                break;
        }
    }

    private void HandleListInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (_selectedSkill > 0)
                {
                    _selectedSkill--;
                    UpdateDetailView();
                    Draw();
                }
                break;
            case ConsoleKey.DownArrow:
                if (_selectedSkill < _skills.Count - 1)
                {
                    _selectedSkill++;
                    UpdateDetailView();
                    Draw();
                }
                break;
            case ConsoleKey.Enter:
                _focus = FocusArea.Detail;
                Draw();
                break;
        }
    }

    private void HandleDetailInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (_detailScroll > 0) { _detailScroll--; Draw(); }
                break;
            case ConsoleKey.DownArrow:
                if (_detailScroll < _detailLines.Count - 5) { _detailScroll++; Draw(); }
                break;
            case ConsoleKey.PageUp:
                _detailScroll = Math.Max(0, _detailScroll - 10);
                Draw();
                break;
            case ConsoleKey.PageDown:
                _detailScroll = Math.Min(Math.Max(0, _detailLines.Count - 5), _detailScroll + 10);
                Draw();
                break;
        }
    }

    private void UpdateDetailView()
    {
        _detailLines.Clear();
        _detailScroll = 0;

        if (_selectedSkill < 0 || _selectedSkill >= _skills.Count) return;

        var skill = _skills[_selectedSkill];
        _detailLines.Add($"{skill.Name} v{skill.Version}");
        _detailLines.Add($"By: {skill.Author}");
        _detailLines.Add($"Tags: {string.Join(", ", skill.Tags)}");
        _detailLines.Add($"Downloads: {skill.DownloadCount}");
        _detailLines.Add("");
        _detailLines.Add("Required Tool Groups:");

        foreach (var g in skill.RequiredToolGroups)
        {
            var elevated = ToolPermissions.IsElevated(g) ? " [ELEVATED]" : "";
            _detailLines.Add($"  - {g}{elevated}");
        }

        _detailLines.Add("");
        // Wrap description
        foreach (var line in skill.Description.Split('\n'))
        {
            if (line.Length <= 35)
                _detailLines.Add(line);
            else
            {
                var remaining = line;
                while (remaining.Length > 35)
                {
                    var bp = remaining.LastIndexOf(' ', 35);
                    if (bp <= 0) bp = 35;
                    _detailLines.Add(remaining[..bp]);
                    remaining = remaining[bp..].TrimStart();
                }
                if (remaining.Length > 0)
                    _detailLines.Add(remaining);
            }
        }
    }
}
