using System.Threading.Tasks;

namespace TaskDrivenAgent.Core.Interfaces
{
    public interface IAgentTool
    {
        string Name { get; }
        string Description { get; }
        string ParametersDescription { get; }
        Task<string> ExecuteAsync(string arguments, string workspacePath);
    }
}
