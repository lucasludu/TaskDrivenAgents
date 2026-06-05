using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using TaskDrivenAgent.Core.Interfaces;

namespace TaskDrivenAgent.Tools
{
    public class ExploreDirectoryTool : IAgentTool
    {
        public string Name => "ExploreDirectory";
        public string Description => "Lists all files and subdirectories in a given directory relative to the workspace.";
        public string ParametersDescription => "Relative path to the directory to explore. Pass an empty string or '.' to explore the root workspace directory.";

        public Task<string> ExecuteAsync(string arguments, string workspacePath)
        {
            var relativePath = arguments.Trim().Trim('"', '\'');
            if (relativePath == "." || relativePath == "/") relativePath = "";

            var targetDir = Path.GetFullPath(Path.Combine(workspacePath, relativePath));

            // Security: Prevent path traversal escaping the workspace
            if (!targetDir.StartsWith(Path.GetFullPath(workspacePath), StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult("Error: Path traversal outside workspace is forbidden.");
            }

            if (!Directory.Exists(targetDir))
            {
                return Task.FromResult($"Error: Directory '{relativePath}' does not exist.");
            }

            try
            {
                var dirs = Directory.GetDirectories(targetDir).Select(d => "[DIR] " + Path.GetFileName(d)).ToList();
                var files = Directory.GetFiles(targetDir).Select(f => Path.GetFileName(f)).ToList();

                var allItems = dirs.Concat(files).ToList();
                
                if (allItems.Count == 0)
                    return Task.FromResult("Directory is empty.");

                return Task.FromResult(string.Join("\n", allItems));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error reading directory: {ex.Message}");
            }
        }
    }
}
