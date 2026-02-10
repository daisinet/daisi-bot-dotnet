# DaisiBot .NET

A multi-platform AI chatbot client for the [DAISI](https://daisinet.com) decentralized inference network. DaisiBot provides three interfaces — a terminal UI, a native desktop/mobile app, and a web application — all powered by a shared service layer that streams responses from DAISI-hosted language models.

## Architecture

```
DaisiBot.slnx
│
├── DaisiBot.Core          Domain models, enums, service interfaces
├── DaisiBot.Agent         DAISI SDK integration (auth, chat, models)
├── DaisiBot.Data          SQLite persistence via EF Core
├── DaisiBot.Shared.UI     Reusable Blazor/MudBlazor components
│
├── DaisiBot.Tui           Terminal UI (raw ANSI console)
├── DaisiBot.Maui          Native app (Blazor Hybrid — Windows, macOS, iOS, Android)
└── DaisiBot.Web           Web app (ASP.NET Blazor Server + skill marketplace)
```

All projects target **.NET 10.0**.

## Projects

### DaisiBot.Core

Shared domain layer with no external dependencies. Defines:

- **Models**: `Conversation`, `ChatMessage`, `AgentConfig`, `AuthState`, `UserSettings`, `Skill`, `AvailableModel`, `ChatStats`, `BotInstance`, `BotLogEntry`, `SlashCommand`
- **Interfaces**: `IAuthService`, `IChatService`, `IConversationStore`, `ISettingsService`, `IModelService`, `ISkillService`, `IBotEngine`, `IBotStore`
- **Enums**: `ConversationThinkLevel` (Basic → TreeOfThought), `ChatMessageType` (Text, Thinking, Tooling, Error, etc.), `ToolGroupSelection` (8 categories of agent tools), `ChatRole`, `SkillStatus`, `SkillVisibility`, `BotStatus` (Idle/Running/WaitingForInput/Completed/Failed/Stopped), `BotScheduleType` (Once/Continuous/Interval/Hourly/Daily), `BotLogLevel`
- **Security**: `ToolPermissions` for elevated access control, `EmployeeCheck` for internal authorization

### DaisiBot.Agent

Service implementations that integrate with the DAISI SDK via gRPC:

- **DaisiBotAuthService** — Two-step authentication (email/phone → verification code). Persists tokens to SQLite and feeds them to the SDK's client key provider.
- **DaisiBotChatService** — Streaming chat via `IAsyncEnumerable<StreamChunk>`. Creates inference sessions with configurable model, think level, temperature, top-P, max tokens, tool groups, and skill prompts. Processes response chunks (text, thinking, tooling, errors), cleans content, calculates stats (tokens/sec), and persists messages.
- **DaisiBotModelService** — Queries available models from the network and enriches them with supported think levels (reasoning models unlock Chain of Thought and Tree of Thought).
- **ContentCleaner** — Strips internal XML tags (`<think>`, `<response>`) and anti-prompts from model output.
- **SkillPromptBuilder** — Injects enabled skill system prompt templates into the conversation context.
- **EnumMapper** — Bidirectional mapping between Core enums and DAISI protobuf types.

**Bot Engine** (`BotEngine.cs`):
- Autonomous bot execution with plan-execute-synthesize loop
- AI-generated action plans broken into 2-5 steps, each executed with tool access
- Scheduler timer checks for runnable bots every 30 seconds (first check 2s after startup)
- Five schedule types: Once, Continuous, Interval (custom minutes), Hourly, Daily
- Failure recovery: on error, prompts user for guidance via `WaitForInputAsync`, stores response as `RetryGuidance` that is injected into planning and execution prompts on retry
- `WaitForInputAsync` blocks execution with a `TaskCompletionSource` until user responds
- Concurrent bot management via `ConcurrentDictionary<Guid, BotRuntime>` with per-bot cancellation tokens and output channels

DI registration is handled by `AddDaisiBotAgent()` extension method.

### DaisiBot.Data

SQLite database layer using EF Core. Database location: `%LocalAppData%\DaisiBot\daisibot.db`.

- **DaisiBotDbContext** — DbSets for Conversations, Messages, Settings, AuthStates, InstalledSkills, Bots, BotLogEntries. Conversations cascade-delete their messages. `ApplyMigrationsAsync()` handles schema drift with ALTER TABLE migrations for new columns.
- **SqliteConversationStore** — CRUD for conversations and messages, ordered by last update.
- **SqliteAuthStateStore** — Singleton upsert pattern (ID=1) for auth token persistence.
- **SqliteSettingsService** — Singleton upsert for user preferences (includes `LastScreen` for UI state persistence).
- **SqliteInstalledSkillStore** — Tracks installed skills per account.
- **SqliteBotStore** — CRUD for bot instances and log entries, with `GetRunnableAsync()` for scheduler queries.
- **SqliteSkillService** — Skill marketplace queries.

### DaisiBot.Shared.UI

Reusable Blazor components built with [MudBlazor](https://mudblazor.com) (Material Design):

- **Chat**: `ChatPanel` (message display + streaming + input), `ChatMessageBubble`, `StreamingMessage`, `ChatStatsBar`, `ChatInput`, `ThinkingIndicator`
- **Auth**: `AuthStatusBadge`, `LoginDialog`
- **Layout**: `MainLayout` (app shell with sidebar), `NavSidebar` (conversation list + navigation)
- **Settings**: `SettingsDialog`, `ModelPicker`, `ThinkLevelSelector`, `ToolGroupSelector`
- **Skills**: `SkillBrowser`, `SkillCard`, `SkillDetail`, `InstalledSkillsList`, `SkillSubmitForm`
- **Services**: `ChatNavigationState` — singleton bridging sidebar selection and chat panel rendering

### DaisiBot.Tui

Terminal-based client using raw `System.Console` and ANSI escape codes (no external TUI library).

**Event Loop** (`App.cs`):
- Single-threaded main loop polling `Console.KeyAvailable` at ~60Hz
- `ConcurrentQueue<Action>` for thread-safe UI updates from async tasks
- `App.Post(Action)` lets background operations safely update the display
- Modal dialog stack (`IModal`) for layered UI
- `IScreen` interface with `Draw()`, `HandleKey()`, and `Activate()` for screen lifecycle
- Windows VT100 terminal processing enabled via P/Invoke

**Rendering** (`AnsiConsole.cs`):
- Cursor positioning, foreground/background colors (including RGB), bold/dim/reverse/underline styles
- Unicode box-drawing characters for borders and panels
- Alternate screen buffer for clean terminal restore on exit

**Screen Router** (`ScreenRouter.cs`):
- F1/F2 switches between Bots and Chats screens, F10 quits
- Persists last active screen to `UserSettings.LastScreen` — restored on next launch
- Default screen: Bots

**Layout** — Two screens sharing the same structure:
```
Row 0:       Title bar
Row 1..H-2:  [Sidebar 24 cols] | [Content panel, rest of width]
Row H-1:     Status bar (F1:Bots F2:Chats F3:Model F4:Settings F5:Login F6:Skills F10:Quit)
```

**Bot Screen** (`BotMainScreen.cs`):
- `BotSidebarPanel` — Bot list with status icons (▶ Running, ○ Idle, ⚠ WaitingForInput with flash animation, ✓ Completed, ✗ Failed, ■ Stopped)
- `BotOutputPanel` — Scrollable log output with color-coded entries: bold magenta for steps, bold cyan with background highlight for results, bold red for errors, yellow for warnings. Left padding, input sanitization to prevent sidebar overwrite. Visible cursor at typing position.

**Chat Screen** (`MainScreen.cs`):
- `SidebarPanel` — Conversation list with keyboard navigation (Up/Down/Enter/N/Delete)
- `ChatPanel` — Word-wrapped message display with scrolling, full line editing, streaming output

**Shared UI Features**:
- Active panel border highlights in green, title text bold green when focused
- Tab toggles focus between sidebar and content panel
- Selecting an item in the sidebar auto-focuses the command line
- First item auto-selected on screen activation; focus returns to sidebar when last item is deleted

**Slash Command System**:
- `SlashCommandPopup` — Inline autocomplete popup above the input line, showing top 5 matching commands as user types after `/`. Up/Down to navigate, Tab/Enter to complete. Commands without parameters auto-execute on selection.
- `SlashCommandDispatcher` — Routes commands to handlers. Context-aware: `CurrentBot`/`CurrentConversation` set on selection change.
- **Commands**: `/help`, `/new`, `/list`, `/status`, `/kill`, `/runnow`, `/clear`, `/model`, `/settings`, `/skills`, `/export`, `/install <skill>`, `/login`
- `/kill` — Context-aware, shows confirmation dialog, stops and deletes the bot
- `/runnow` — Runs the selected bot immediately without affecting its schedule
- `/status` — Shows detailed status of the selected bot or all bots

**Dialogs**: `LoginFlow`, `ModelPickerFlow`, `SettingsFlow`, `SkillBrowserFlow`, `BotCreationFlow`, `HelpModal`, `ConfirmDialog` — all modal, async operations run on thread pool and post results back via `App.Post()`

**Bot Creation Flow** (`BotCreationFlow.cs`):
Multi-step wizard: Goal → Label → Persona → Schedule → (Interval minutes if applicable) → Start Mode (Run Immediately / Schedule First Run) → Create

### DaisiBot.Maui

Cross-platform native app using .NET MAUI Blazor Hybrid. Targets Windows (WinUI), macOS (Mac Catalyst), iOS, and Android.

- `BlazorWebView` hosts Razor components from `DaisiBot.Shared.UI`
- MudBlazor provides the Material Design UI
- Page-based routing: `/login` (standalone auth page with `EmptyLayout`), `/` (chat), `/settings`, `/skills`
- `ChatNavigationState` singleton coordinates conversation selection between sidebar and chat panel
- Auto-creates conversations on first message
- Auth redirect: unauthenticated users are sent to `/login`, logout navigates back

### DaisiBot.Web

ASP.NET Core Blazor Server application with skill marketplace features.

- Cookie-based authentication via `AddDaisiForWeb()` / `AddDaisiCookieKeyProvider()`
- **CosmoSkillService** — `ISkillService` implementation backed by Cosmos DB (via `Daisi.Orc.Core`)
  - Public skill marketplace with search and tag filtering
  - Skill creation, publishing, and review workflow (Draft → PendingReview → Approved/Rejected)
  - Per-account skill installation tracking
- Interactive server-side rendering with MudBlazor
- Configuration loaded from `appsettings.json` (DAISI network, Cosmos DB connection)

## Key Concepts

### Authentication

Two-step OTP flow:
1. User provides email or phone number → `SendAuthCodeAsync()` sends a verification code
2. User enters code → `ValidateAuthCodeAsync()` returns a client key (token)
3. Token is persisted in SQLite and injected into all DAISI SDK calls via `DaisiBotClientKeyProvider`

### Streaming Chat

Messages are sent and responses streamed using `IAsyncEnumerable<StreamChunk>`:

```
User message → Save to DB → Create inference session → Stream chunks → Clean content → Save response → Update stats
```

Each chunk is typed (Text, Thinking, Tooling, ToolContent, Error, Image, Audio) enabling the UI to render thinking processes and tool usage distinctly from final responses.

### Think Levels

Progressive reasoning depth:
- **Basic** — Direct inference
- **Basic + Tools** — Inference with tool group access
- **Chain of Thought** — Extended reasoning (visible to user)
- **Tree of Thought** — Deep exploration with branching (reasoning models only)

### Tool Groups

Eight categories of agent capabilities: Information, File, Math, Communication, Coding, Media, Integration, Social. Enabled per-conversation via settings. Skills can declare required tool groups.

### Skills

Versioned, authored components with system prompt templates that inject into conversations:
- Users browse a marketplace of approved public skills
- Install/uninstall skills per account
- Installed skills' prompts are concatenated into the conversation system prompt
- Creation workflow: Draft → Submit for Review → Approved/Rejected

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- For MAUI: appropriate workloads (`dotnet workload install maui`)

### Build

```bash
dotnet build DaisiBot.slnx
```

### Run the TUI

```bash
dotnet run --project src/DaisiBot.Tui
```

Press F4 to log in, then start chatting. Use F2 to pick a model, F3 for settings, F5 to browse skills, F10 to quit.

### Run the MAUI App

```bash
dotnet run --project src/DaisiBot.Maui -f net10.0-windows10.0.19041.0
```

The login page appears on startup. After authentication, use the sidebar to manage conversations and navigate between Chat, Settings, and Skills pages.

### Run the Web App

```bash
dotnet run --project src/DaisiBot.Web
```

Configure DAISI network and Cosmos DB settings in `appsettings.json`.

## Data Storage

All client apps (TUI, MAUI) store data locally in SQLite at:
- **Windows**: `%LocalAppData%\DaisiBot\daisibot.db`
- **macOS/Linux**: `~/.local/share/DaisiBot/daisibot.db`

The Web app uses Cosmos DB for the skill marketplace and cookie-based sessions for auth.

## License

Proprietary. All rights reserved.
