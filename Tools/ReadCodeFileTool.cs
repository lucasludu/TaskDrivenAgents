using System;
using System.IO;
using System.Threading.Tasks;

using TaskDrivenAgent.Core.Interfaces;

namespace TaskDrivenAgent.Tools
{
    public class ReadCodeFileTool : IAgentTool
    {
        public string Name => "ReadCodeFile";
        public string Description => "Reads the contents of a file relative to the workspace.";
        public string ParametersDescription => "Relative path to the file to read (e.g., 'src/Program.cs').";

        public async Task<string> ExecuteAsync(string arguments, string workspacePath)
        {
            var relativePath = arguments.Trim().Trim('"', '\'');
            var targetFile = Path.GetFullPath(Path.Combine(workspacePath, relativePath));

            // Security: Prevent path traversal
            if (!targetFile.StartsWith(Path.GetFullPath(workspacePath), StringComparison.OrdinalIgnoreCase))
            {
                return "Error: Path traversal outside workspace is forbidden.";
            }

            if (!File.Exists(targetFile))
            {
                return $"Error: File '{relativePath}' does not exist.";
            }

            try
            {
                var content = await File.ReadAllTextAsync(targetFile);
                return content;
            }
            catch (Exception ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }
    }
}
