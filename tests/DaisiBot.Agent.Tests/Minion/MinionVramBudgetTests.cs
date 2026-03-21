using DaisiBot.Agent.Minion;
using Daisi.Llogos.Model;

namespace DaisiBot.Agent.Tests.Minion;

public class MinionVramBudgetTests
{
    /// <summary>
    /// Create a realistic ModelConfig for testing. Mimics a small Qwen-style model.
    /// </summary>
    private static ModelConfig CreateTestConfig(
        int numLayers = 24,
        int hiddenDim = 1024,
        int intermediateDim = 2816,
        int vocabSize = 151936,
        int numHeads = 16,
        int numKvHeads = 4,
        int keyLength = 64,
        int valueLength = 64,
        int fullAttentionInterval = 0,
        int ssmGroupCount = 0,
        int ssmInnerSize = 0,
        int ssmConvKernel = 0,
        int ssmStateSize = 0)
    {
        return new ModelConfig
        {
            Architecture = "qwen2",
            NumLayers = numLayers,
            HiddenDim = hiddenDim,
            IntermediateDim = intermediateDim,
            VocabSize = vocabSize,
            MaxContext = 32768,
            NormEps = 1e-6f,
            NumHeads = numHeads,
            NumKvHeads = numKvHeads,
            KeyLength = keyLength,
            ValueLength = valueLength,
            RopeTheta = 10000f,
            RopeDimCount = keyLength,
            FullAttentionInterval = fullAttentionInterval,
            SsmConvKernel = ssmConvKernel,
            SsmStateSize = ssmStateSize,
            SsmGroupCount = ssmGroupCount,
            SsmInnerSize = ssmInnerSize,
        };
    }

    [Fact]
    public void EstimateSessionBytes_ReturnsPositive()
    {
        var config = CreateTestConfig();
        var bytes = MinionVramBudget.EstimateSessionBytes(config, 2048);

        Assert.True(bytes > 0);
    }

    [Fact]
    public void EstimateSessionBytes_LargerContext_MoreMemory()
    {
        var config = CreateTestConfig();
        var small = MinionVramBudget.EstimateSessionBytes(config, 1024);
        var large = MinionVramBudget.EstimateSessionBytes(config, 4096);

        Assert.True(large > small, $"Expected 4096 ctx ({large}) > 1024 ctx ({small})");
    }

    [Fact]
    public void EstimateSessionBytes_IncludesKvCache()
    {
        var config = CreateTestConfig(numLayers: 24, numKvHeads: 4, keyLength: 64, valueLength: 64);
        var bytes = MinionVramBudget.EstimateSessionBytes(config, 2048);

        // KV cache alone: 24 layers * 4 heads * 2048 positions * (64 + 64) bytes_per_pos * 2 (F16)
        // = 24 * 4 * 2048 * 128 * 2 = 50,331,648 bytes (~48 MB)
        long minExpectedKv = 24L * 4 * 2048 * (64 + 64) * 2;
        Assert.True(bytes >= minExpectedKv,
            $"Session bytes ({bytes}) should be at least KV cache ({minExpectedKv})");
    }

    [Fact]
    public void EstimateSessionBytes_WithDeltaNet_IncludesStateMemory()
    {
        // Model with hybrid attention/DeltaNet layers (like Qwen3.5 9B)
        var configNoDelta = CreateTestConfig();
        var configWithDelta = CreateTestConfig(
            fullAttentionInterval: 6,  // every 6th layer is attention, rest are DeltaNet
            ssmGroupCount: 16,
            ssmInnerSize: 1024,
            ssmConvKernel: 4,
            ssmStateSize: 64);

        var bytesNoDelta = MinionVramBudget.EstimateSessionBytes(configNoDelta, 2048);
        var bytesWithDelta = MinionVramBudget.EstimateSessionBytes(configWithDelta, 2048);

        // DeltaNet model should use different (not necessarily more) memory due to state vs KV tradeoff
        // But it should still be positive
        Assert.True(bytesWithDelta > 0);
    }

    [Fact]
    public void Calculate_ReturnsPositiveMinions_WithEnoughVram()
    {
        var config = CreateTestConfig();
        long freeVram = 4L * 1024 * 1024 * 1024; // 4 GB

        var result = MinionVramBudget.Calculate(config, freeVram, summonerContextSize: 4096);

        Assert.True(result.MaxMinions > 0, $"Expected at least 1 minion with 4GB free. Summary: {result.Summary}");
        Assert.True(result.ContextPerMinion > 0);
        Assert.True(result.PerSessionBytes > 0);
        Assert.NotEmpty(result.Summary);
    }

    [Fact]
    public void Calculate_ReturnsZeroMinions_WithNoVram()
    {
        var config = CreateTestConfig();
        long freeVram = 100 * 1024 * 1024; // 100 MB (less than the 512MB reserve)

        var result = MinionVramBudget.Calculate(config, freeVram, summonerContextSize: 4096);

        Assert.Equal(0, result.MaxMinions);
        Assert.NotEmpty(result.Summary);
    }

    [Fact]
    public void Calculate_MoreVram_MoreMinions()
    {
        var config = CreateTestConfig();

        var small = MinionVramBudget.Calculate(config, 2L * 1024 * 1024 * 1024, summonerContextSize: 4096);
        var large = MinionVramBudget.Calculate(config, 8L * 1024 * 1024 * 1024, summonerContextSize: 4096);

        Assert.True(large.MaxMinions >= small.MaxMinions,
            $"8GB ({large.MaxMinions}) should allow >= minions than 2GB ({small.MaxMinions})");
    }

    [Fact]
    public void Calculate_SmallerContext_MoreMinions()
    {
        var config = CreateTestConfig();
        long freeVram = 4L * 1024 * 1024 * 1024;

        var smallCtx = MinionVramBudget.Calculate(config, freeVram, summonerContextSize: 4096, targetMinionContextSize: 512);
        var largeCtx = MinionVramBudget.Calculate(config, freeVram, summonerContextSize: 4096, targetMinionContextSize: 4096);

        Assert.True(smallCtx.MaxMinions >= largeCtx.MaxMinions,
            $"512 ctx ({smallCtx.MaxMinions}) should allow >= minions than 4096 ctx ({largeCtx.MaxMinions})");
    }

    [Fact]
    public void Calculate_DefaultMinionContext_CappedAt2048()
    {
        var config = CreateTestConfig();
        long freeVram = 4L * 1024 * 1024 * 1024;

        // Summoner has a large context but default minion ctx should cap at 2048
        var result = MinionVramBudget.Calculate(config, freeVram, summonerContextSize: 32768);

        Assert.Equal(2048, result.ContextPerMinion);
    }

    [Fact]
    public void Calculate_Summary_ContainsUsefulInfo()
    {
        var config = CreateTestConfig();
        long freeVram = 4L * 1024 * 1024 * 1024;

        var result = MinionVramBudget.Calculate(config, freeVram, summonerContextSize: 4096);

        Assert.Contains("Max", result.Summary);
        Assert.Contains("minion", result.Summary);
        Assert.Contains("ctx", result.Summary);
    }
}
