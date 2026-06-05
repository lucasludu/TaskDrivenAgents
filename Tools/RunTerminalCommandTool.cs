using System;
using System.Diagnostics;
using System.Threading.Tasks;

using TaskDrivenAgent.Core.Interfaces;

namespace TaskDrivenAgent.Tools
{
    public class RunTerminalCommandTool : IAgentTool
    {
        public string Name => "RunTerminalCommand";
        public string Description => "Executes a shell command (PowerShell on Windows, Bash on Linux) in the workspace directory and returns its stdout and stderr.";
        public string ParametersDescription => "The command string to execute (e.g., 'dotnet build' or 'npm test').";

        public async Task<string> ExecuteAsync(string arguments, string workspacePath)
        {
            var command = arguments.Trim();
            if (string.IsNullOrWhiteSpace(command))
            {
                return "Error: Command cannot be empty.";
            }

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    WorkingDirectory = workspacePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (OperatingSystem.IsWindows())
                {
                    processInfo.FileName = "powershell.exe";
                    processInfo.Arguments = $"-NoProfile -Command \"{command.Replace("\"", "\\\"")}\"";
                }
                else
                {
                    processInfo.FileName = "bash";
                    processInfo.Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"";
                }

                using var process = new Process { StartInfo = processInfo };
                process.Start();

                // Wait for the process to finish, but with a timeout of 60 seconds to prevent hanging the agent
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(60));
                var exitTask = process.WaitForExitAsync();

                var completedTask = await Task.WhenAny(exitTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    process.Kill();
                    return "Error: Command execution timed out after 60 seconds.";
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                var result = "";
                if (!string.IsNullOrWhiteSpace(output))
                {
                    result += $"[STDOUT]\n{output}\n";
                }
                if (!string.IsNullOrWhiteSpace(error))
                {
                    result += $"[STDERR]\n{error}\n";
                }

                result += $"[EXIT CODE]: {process.ExitCode}";
                return result;
            }
            catch (Exception ex)
            {
                return $"Error executing command: {ex.Message}";
            }
        }
    }
}
