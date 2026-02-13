using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;

namespace DaisiBot.LocalTools.Tests.Helpers
{
    public class MockToolContext : IToolContext
    {
        private readonly Func<SendInferenceRequest, Task<SendInferenceResponse>>? _inferCallback;

        public List<SendInferenceRequest> InferRequests { get; } = new();

        public IServiceProvider Services { get; }

        public MockToolContext(
            Func<SendInferenceRequest, Task<SendInferenceResponse>>? inferCallback = null,
            IServiceProvider? services = null)
        {
            _inferCallback = inferCallback;
            Services = services ?? new EmptyServiceProvider();
        }

        public async Task<SendInferenceResponse> InferAsync(SendInferenceRequest request)
        {
            InferRequests.Add(request);

            if (_inferCallback is not null)
                return await _inferCallback(request);

            return new SendInferenceResponse { Content = "Mock inference response" };
        }

        private class EmptyServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}
