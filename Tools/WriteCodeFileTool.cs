using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using TaskDrivenAgent.Core.Interfaces;

namespace TaskDrivenAgent.Tools
{
    public class WriteCodeFileTool : IAgentTool
    {
        public string Name => "WriteCodeFile";
        public string Description => "Creates or overwrites a file with the given content.";
        public string ParametersDescription => "A JSON string with 'filePath' (relative to workspace) and 'content' properties. Example: {\"filePath\": \"test.cs\", \"content\": \"code\"}";

        public async Task<string> ExecuteAsync(string arguments, string workspacePath)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var payload = JsonSerializer.Deserialize<WritePayload>(arguments, options);

                if (payload == null || string.IsNullOrWhiteSpace(payload.FilePath))
                {
                    return "Error: Invalid JSON arguments. Expected {\"filePath\": \"...\", \"content\": \"...\"}";
                }

                var targetFile = Path.GetFullPath(Path.Combine(workspacePath, payload.FilePath));

                // Security: Prevent path traversal
                if (!targetFile.StartsWith(Path.GetFullPath(workspacePath), StringComparison.OrdinalIgnoreCase))
                {
                    return "Error: Path traversal outside workspace is forbidden.";
                }

                var directory = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(targetFile, payload.Content ?? "");
                return $"Success: File '{payload.FilePath}' was written successfully.";
            }
            catch (JsonException)
            {
                return "Error: Arguments must be a valid JSON object with 'filePath' and 'content'.";
            }
            catch (Exception ex)
            {
                return $"Error writing file: {ex.Message}";
            }
        }

        private class WritePayload
        {
            public string FilePath { get; set; } = "";
            public string Content { get; set; } = "";
        }
    }
}
