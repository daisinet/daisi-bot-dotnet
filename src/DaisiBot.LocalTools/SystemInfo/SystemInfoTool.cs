using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DaisiBot.LocalTools.SystemInfo
{
    public class SystemInfoTool : DaisiToolBase
    {
        private const string P_CATEGORY = "category";

        public override string Id => "daisi-system-info";
        public override string Name => "Daisi System Info";

        public override string UseInstructions =>
            "Use this tool to get system information: OS, CPU, memory, and disk details. " +
            "Keywords: system info, computer info, os info, hardware info, specs.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_CATEGORY, Description = "Category: all, os, cpu, memory, or disk. Default is all.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var category = parameters.GetParameterValueOrDefault(P_CATEGORY, "all");

            return new ToolExecutionContext
            {
                ExecutionMessage = "Gathering system information",
                ExecutionTask = Task.Run(() => GetSystemInfo(category))
            };
        }

        private static ToolResult GetSystemInfo(string category)
        {
            try
            {
                var info = new Dictionary<string, object>();

                if (category is "all" or "os")
                {
                    info["os"] = new
                    {
                        description = RuntimeInformation.OSDescription,
                        architecture = RuntimeInformation.OSArchitecture.ToString(),
                        machineName = Environment.MachineName,
                        userName = Environment.UserName,
                        dotnetVersion = Environment.Version.ToString(),
                        processorCount = Environment.ProcessorCount
                    };
                }

                if (category is "all" or "cpu")
                {
                    info["cpu"] = new
                    {
                        processorCount = Environment.ProcessorCount,
                        architecture = RuntimeInformation.ProcessArchitecture.ToString()
                    };
                }

                if (category is "all" or "memory")
                {
                    var gcInfo = GC.GetGCMemoryInfo();
                    info["memory"] = new
                    {
                        totalAvailableMemoryBytes = gcInfo.TotalAvailableMemoryBytes,
                        totalAvailableMemoryMB = gcInfo.TotalAvailableMemoryBytes / (1024 * 1024),
                        gcHeapSizeBytes = GC.GetTotalMemory(false),
                        gcHeapSizeMB = GC.GetTotalMemory(false) / (1024 * 1024)
                    };
                }

                if (category is "all" or "disk")
                {
                    var drives = DriveInfo.GetDrives()
                        .Where(d => d.IsReady)
                        .Select(d => new
                        {
                            name = d.Name,
                            label = d.VolumeLabel,
                            format = d.DriveFormat,
                            type = d.DriveType.ToString(),
                            totalSizeGB = d.TotalSize / (1024L * 1024 * 1024),
                            freeSpaceGB = d.AvailableFreeSpace / (1024L * 1024 * 1024)
                        }).ToArray();
                    info["disk"] = drives;
                }

                var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
                return new ToolResult
                {
                    Success = true,
                    Output = json,
                    OutputFormat = InferenceOutputFormats.Json,
                    OutputMessage = "System information retrieved"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
