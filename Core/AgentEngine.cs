using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskDrivenAgent.Core.Interfaces;
using TaskDrivenAgent.Core.Models;
using TaskDrivenAgent.Services;

namespace TaskDrivenAgent.Core
{
    public class AgentEngine
    {
        private readonly ILlmService _llmService;
        private readonly Dictionary<string, IAgentTool> _tools;

        public AgentEngine(ILlmService llmService, IEnumerable<IAgentTool> tools)
        {
            _llmService = llmService;
            _tools = tools.ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<string> RunAsync(string objective, string workspacePath, int maxSteps = 10, Func<AgentStep, Task>? onStepAdded = null)
        {
            // Create a System Prompt describing the agent's behavior and tools
            var toolsDescription = string.Join("\n", _tools.Values.Select(t => 
                $"- {t.Name}: {t.Description}\n  Parameters: {t.ParametersDescription}"));

            var systemPrompt = $@"You are a Task-Driven Autonomous Agent.
Your objective is to solve the task given by the user using a step-by-step loop of Thought, Action, and Observation (ReAct framework).
You are currently operating within the workspace directory: {workspacePath}
All file paths you use with tools MUST be relative to this workspace directory.

At each step, you must format your output exactly as follows:
Thought: [Describe your reasoning about what to do next]
Action: [ToolName]([arguments])

Available Tools:
{toolsDescription}

When you have achieved the objective, instead of calling a tool, you must write:
Thought: [Describe your final reasoning]
Final Answer: [Write your final answer or summary of completion here]

Important Rules:
1. Always start your response with 'Thought:'.
2. Do not call multiple tools at once. Only call one Action per response.
3. Make sure to pass parameters in the format expected by the tool (some tools expect JSON objects, others expect plain strings).
4. Be precise and thorough.
5. When calling a tool, pass ONLY the raw value inside the parentheses. Do NOT write parameter names like 'fileName=' or use variable assignments (e.g., use ReadLogFile(production-errors.log) instead of ReadLogFile(fileName='production-errors.log')).
";

            var messages = new List<ChatMessage>
            {
                new ChatMessage("system", systemPrompt),
                new ChatMessage("user", $"Objective: {objective}")
            };

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=========================================================");
            Console.WriteLine("🤖 MOTOR DE AGENTE AUTÓNOMO INICIADO");
            Console.WriteLine($"Objetivo: \"{objective}\"");
            Console.WriteLine("=========================================================");
            Console.ResetColor();

            for (int step = 1; step <= maxSteps; step++)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n[Paso {step}/{maxSteps}] Consultando al LLM...");
                Console.ResetColor();

                // Get LLM response
                string response = await _llmService.GetCompletionAsync(messages);
                
                // Add Assistant response to history
                messages.Add(new ChatMessage("assistant", response));

                // Parse Thought
                var thought = ParseThought(response);
                if (!string.IsNullOrEmpty(thought))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("🤔 PENSAMIENTO: ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(thought);
                    Console.ResetColor();

                    if (onStepAdded != null)
                    {
                        await onStepAdded(new AgentStep(AgentStepType.Thought, thought, DateTime.Now));
                    }
                }

                // Check for Final Answer
                var finalAnswer = ParseFinalAnswer(response);
                if (!string.IsNullOrEmpty(finalAnswer))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n🏆 [OBJETIVO COMPLETADO]");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(finalAnswer);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("=========================================================");
                    Console.ResetColor();

                    if (onStepAdded != null)
                    {
                        await onStepAdded(new AgentStep(AgentStepType.FinalAnswer, finalAnswer, DateTime.Now));
                    }
                    return finalAnswer;
                }

                // Parse Action
                var action = ParseAction(response);
                if (action != null)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("🛠️  ACCIÓN: ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"Llamando a '{action.Value.ToolName}' con argumentos: {action.Value.Arguments}");
                    Console.ResetColor();

                    if (onStepAdded != null)
                    {
                        await onStepAdded(new AgentStep(AgentStepType.Action, $"Llamando a '{action.Value.ToolName}' con argumentos: {action.Value.Arguments}", DateTime.Now));
                    }

                    if (_tools.TryGetValue(action.Value.ToolName, out var tool))
                    {
                        try
                        {
                            // Execute tool
                            string observation = await tool.ExecuteAsync(action.Value.Arguments, workspacePath);
                            
                            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            Console.Write("👁️  OBSERVACIÓN: ");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(observation);
                            Console.ResetColor();

                            // Add observation back to history as user message so the agent reads it
                            messages.Add(new ChatMessage("user", observation));

                            if (onStepAdded != null)
                            {
                                await onStepAdded(new AgentStep(AgentStepType.Observation, observation, DateTime.Now));
                            }
                        }
                        catch (Exception ex)
                        {
                            string errObs = $"Error executing tool {tool.Name}: {ex.Message}";
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"❌ {errObs}");
                            Console.ResetColor();
                            messages.Add(new ChatMessage("user", $"Observation: {errObs}"));

                            if (onStepAdded != null)
                            {
                                await onStepAdded(new AgentStep(AgentStepType.Error, errObs, DateTime.Now));
                            }
                        }
                    }
                    else
                    {
                        string errObs = $"Error: Tool '{action.Value.ToolName}' not found.";
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ {errObs}");
                        Console.ResetColor();
                        messages.Add(new ChatMessage("user", $"Observation: {errObs}"));

                        if (onStepAdded != null)
                        {
                            await onStepAdded(new AgentStep(AgentStepType.Error, errObs, DateTime.Now));
                        }
                    }
                }
                else
                {
                    // If LLM returned text but no parseable action or final answer
                    string errObs = "Error: Output did not contain a valid Action: Tool(args) or Final Answer: [result].";
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"⚠️ {errObs}");
                    Console.ResetColor();
                    messages.Add(new ChatMessage("user", $"Observation: {errObs} Please try again using the correct format."));

                    if (onStepAdded != null)
                    {
                        await onStepAdded(new AgentStep(AgentStepType.Error, errObs, DateTime.Now));
                    }
                }
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n❌ Se alcanzó el límite máximo de pasos sin obtener una respuesta final.");
            Console.WriteLine("=========================================================");
            Console.ResetColor();

            if (onStepAdded != null)
            {
                await onStepAdded(new AgentStep(AgentStepType.Error, "Límite de pasos alcanzado sin completar el objetivo.", DateTime.Now));
            }
            return "Límite de pasos alcanzado.";
        }

        private string ParseThought(string response)
        {
            var thoughtMarker = "Thought:";
            var actionMarker = "Action:";
            var finalMarker = "Final Answer:";

            var thoughtIdx = response.IndexOf(thoughtMarker);
            if (thoughtIdx < 0) return "";

            var startIdx = thoughtIdx + thoughtMarker.Length;
            int endIdx = response.Length;

            var nextActionIdx = response.IndexOf(actionMarker, startIdx);
            var nextFinalIdx = response.IndexOf(finalMarker, startIdx);

            if (nextActionIdx >= 0 && nextActionIdx < endIdx) endIdx = nextActionIdx;
            if (nextFinalIdx >= 0 && nextFinalIdx < endIdx) endIdx = nextFinalIdx;

            return response.Substring(startIdx, endIdx - startIdx).Trim();
        }

        private (string ToolName, string Arguments)? ParseAction(string response)
        {
            var actionMarker = "Action:";
            var actionIdx = response.IndexOf(actionMarker);
            if (actionIdx < 0) return null;

            var actionPart = response.Substring(actionIdx + actionMarker.Length);
            var firstParen = actionPart.IndexOf('(');
            var lastParen = actionPart.LastIndexOf(')');

            if (firstParen >= 0 && lastParen > firstParen)
            {
                var toolName = actionPart.Substring(0, firstParen).Trim();
                var toolArgs = actionPart.Substring(firstParen + 1, lastParen - firstParen - 1).Trim();
                return (toolName, toolArgs);
            }

            return null;
        }

        private string ParseFinalAnswer(string response)
        {
            var finalMarker = "Final Answer:";
            var finalIdx = response.IndexOf(finalMarker);
            if (finalIdx < 0) return "";

            return response.Substring(finalIdx + finalMarker.Length).Trim();
        }
    }
}
