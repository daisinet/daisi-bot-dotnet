using Daisi.Llogos.Model;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// Calculates how many in-process minion sessions can fit in available VRAM.
/// Each session needs its own KV cache + DeltaNet state + scratch buffers,
/// but shares the model weights with all other sessions.
/// </summary>
public static class MinionVramBudget
{
    private const long ReserveBytes = 512L * 1024 * 1024; // 512 MB safety margin

    /// <summary>
    /// Estimate VRAM bytes needed for one inference session at a given context size.
    /// </summary>
    public static long EstimateSessionBytes(ModelConfig config, int contextSize)
    {
        // KV cache: only standard attention layers store KV pairs
        int attnLayers = 0;
        int deltaLayers = 0;
        for (int i = 0; i < config.NumLayers; i++)
        {
            if (config.IsStandardAttention(i))
                attnLayers++;
            else
                deltaLayers++;
        }

        // KV cache bytes: each attention layer stores K + V in F16 (2 bytes per element)
        long kvBytesPerLayer = (long)config.NumKvHeads * contextSize * (config.KeyLength + config.ValueLength) * 2;
        long kvBytes = attnLayers * kvBytesPerLayer;

        // DeltaNet state: each delta layer stores state matrix [groupCount x headDim x headDim] in F32
        long deltaBytes = 0;
        if (deltaLayers > 0 && config.SsmGroupCount > 0)
        {
            long stateBytesPerLayer = (long)config.SsmGroupCount * config.SsmHeadDim * config.SsmHeadDim * 4;
            // Conv1d buffer: (convKernel-1) * ssmInnerSize * 3 * 4 bytes (F32)
            long convBytesPerLayer = (long)(config.SsmConvKernel - 1) * config.SsmInnerSize * 3 * 4;
            deltaBytes = deltaLayers * (stateBytesPerLayer + convBytesPerLayer);
        }

        // Scratch buffers for forward pass (rough estimate based on tensor allocations)
        // hidden + residual + normOut + logits + Q/K/V projections + FFN intermediates
        long scratchBytes =
            (config.HiddenDim * 3L                             // hidden, residual, normOut
            + config.VocabSize                                  // logits
            + (long)config.NumHeads * config.KeyLength * 2      // Q (attn + gate)
            + (long)config.NumKvHeads * (config.KeyLength + config.ValueLength)  // K + V projections
            + config.IntermediateDim * 2L                       // FFN gate + up
            ) * 4; // F32

        return kvBytes + deltaBytes + scratchBytes;
    }

    /// <summary>
    /// Calculate the VRAM budget for distributed minion sessions.
    /// </summary>
    /// <param name="config">Model configuration.</param>
    /// <param name="freeVramBytes">Free VRAM reported by GPU detector.</param>
    /// <param name="summonerContextSize">The summoner's own context size (already allocated).</param>
    /// <param name="targetMinionContextSize">Desired context size per minion, or null to use summoner's size.</param>
    public static VramBudgetResult Calculate(
        ModelConfig config,
        long freeVramBytes,
        int summonerContextSize,
        int? targetMinionContextSize = null)
    {
        int minionCtx = targetMinionContextSize ?? Math.Min(summonerContextSize, 2048);
        long perSessionBytes = EstimateSessionBytes(config, minionCtx);

        long available = freeVramBytes - ReserveBytes;
        if (available <= 0)
        {
            return new VramBudgetResult(0, minionCtx, perSessionBytes, available,
                "No VRAM available for minion sessions (reserve exceeds free VRAM).");
        }

        int maxMinions = (int)(available / perSessionBytes);
        if (maxMinions < 1)
        {
            return new VramBudgetResult(0, minionCtx, perSessionBytes, available,
                $"Not enough VRAM for even 1 minion at {minionCtx} context ({FormatBytes(perSessionBytes)} needed, {FormatBytes(available)} available).");
        }

        long totalUsed = maxMinions * perSessionBytes;
        string summary = $"Max {maxMinions} minion{(maxMinions != 1 ? "s" : "")} at {minionCtx} ctx " +
                          $"(each ~{FormatBytes(perSessionBytes)}, {FormatBytes(totalUsed)} total, {FormatBytes(freeVramBytes)} free)";

        return new VramBudgetResult(maxMinions, minionCtx, perSessionBytes, available, summary);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return $"-{FormatBytes(-bytes)}";
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F1}GB";
        return $"{bytes / (1024.0 * 1024):F0}MB";
    }
}

/// <summary>
/// Result of a VRAM budget calculation.
/// </summary>
public sealed record VramBudgetResult(
    int MaxMinions,
    int ContextPerMinion,
    long PerSessionBytes,
    long AvailableBytes,
    string Summary);
