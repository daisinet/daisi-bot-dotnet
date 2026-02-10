namespace DaisiBot.Core.Enums;

public enum BotStatus
{
    Idle,            // created but not running
    Running,         // actively executing or scheduled
    WaitingForInput, // blocked on a user response
    Completed,       // finished
    Failed,          // error
    Stopped          // user-killed
}
