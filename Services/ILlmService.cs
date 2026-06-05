using System.Collections.Generic;
using System.Threading.Tasks;

namespace TaskDrivenAgent.Services
{
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    public interface ILlmService
    {
        string Model { get; set; }
        string ApiKey { get; set; }
        string BaseUrl { get; set; }
        Task<string> GetCompletionAsync(List<ChatMessage> messages);
    }
}
