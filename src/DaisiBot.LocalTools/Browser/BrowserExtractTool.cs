using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.LocalTools.Browser
{
    public class BrowserExtractTool : DaisiToolBase
    {
        private const string P_URL = "url";
        private const string P_MAX_LENGTH = "max-length";

        public override string Id => "daisi-browser-extract";
        public override string Name => "Daisi Browser Extract";

        public override string UseInstructions =>
            "Use this tool to fetch a web page and extract its content as markdown. " +
            "Keywords: extract web page, scrape, fetch page content, read website, get page text.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_URL, Description = "The URL to fetch and extract content from.", IsRequired = true },
            new() { Name = P_MAX_LENGTH, Description = "Maximum character length of output. Default is 50000.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var url = parameters.GetParameter(P_URL).Value;
            var maxLenStr = parameters.GetParameterValueOrDefault(P_MAX_LENGTH, "50000");
            if (!int.TryParse(maxLenStr, out var maxLen)) maxLen = 50000;

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Extracting content from: {url}",
                ExecutionTask = ExtractContent(toolContext, url, maxLen, cancellation)
            };
        }

        private static async Task<ToolResult> ExtractContent(IToolContext toolContext, string url, int maxLen, CancellationToken cancellation)
        {
            try
            {
                var httpClientFactory = toolContext.Services.GetService<IHttpClientFactory>();
                if (httpClientFactory is null)
                    return new ToolResult { Success = false, ErrorMessage = "HttpClientFactory is not available." };

                using var client = httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var html = await client.GetStringAsync(url, cancellation);
                var markdown = HtmlConverter.Convert(html);

                bool truncated = false;
                if (markdown.Length > maxLen)
                {
                    markdown = markdown[..maxLen];
                    truncated = true;
                }

                return new ToolResult
                {
                    Success = true,
                    Output = markdown,
                    OutputFormat = InferenceOutputFormats.Markdown,
                    OutputMessage = truncated
                        ? $"Extracted content (truncated to {maxLen} chars)"
                        : $"Extracted content ({markdown.Length} chars)"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
