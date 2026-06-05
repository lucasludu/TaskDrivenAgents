using System;

namespace TaskDrivenAgent.Core.Models
{
    public enum AgentStepType
    {
        Thought,
        Action,
        Observation,
        FinalAnswer,
        Error
    }

    public record AgentStep(AgentStepType Type, string Content, DateTime Timestamp);
}
