using DaisiBot.Core.Models;

namespace DaisiBot.Core.Interfaces;

public interface IModelService
{
    Task<List<AvailableModel>> GetAvailableModelsAsync();
    Task<AvailableModel?> GetDefaultModelAsync();
}
