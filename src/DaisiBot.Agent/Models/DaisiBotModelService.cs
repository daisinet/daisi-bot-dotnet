using Daisi.SDK.Clients.V1.Orc;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;

namespace DaisiBot.Agent.Models;

public class DaisiBotModelService(ModelClientFactory modelClientFactory) : IModelService
{
    public async Task<List<AvailableModel>> GetAvailableModelsAsync()
    {
        var client = modelClientFactory.Create();
        var response = await client.GetRequiredModelsAsync(new());

        return response.Models.Select(m => new AvailableModel
        {
            Name = m.Name,
            Enabled = m.Enabled,
            IsMultiModal = m.IsMultiModal,
            HasReasoning = m.HasReasoning,
            IsDefault = m.IsDefault,
            SupportedThinkLevels = [
                ConversationThinkLevel.Basic,
                ConversationThinkLevel.BasicWithTools,
                .. m.HasReasoning
                    ? [ConversationThinkLevel.ChainOfThought, ConversationThinkLevel.TreeOfThought]
                    : Array.Empty<ConversationThinkLevel>()
            ]
        }).ToList();
    }

    public async Task<AvailableModel?> GetDefaultModelAsync()
    {
        var models = await GetAvailableModelsAsync();
        return models.FirstOrDefault(m => m.IsDefault) ?? models.FirstOrDefault(m => m.Enabled);
    }
}
