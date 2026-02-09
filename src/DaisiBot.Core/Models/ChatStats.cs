namespace DaisiBot.Core.Models;

public class ChatStats
{
    public int LastMessageTokenCount { get; set; }
    public int SessionTokenCount { get; set; }
    public double LastMessageComputeTimeMs { get; set; }
    public double TokensPerSecond { get; set; }
}
