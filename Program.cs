using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor.Services;
using TaskDrivenAgent.Core;
using TaskDrivenAgent.Core.Interfaces;
using TaskDrivenAgent.Services;
using TaskDrivenAgents.Components;
using TaskDrivenAgent.Tools;

var builder = WebApplication.CreateBuilder(args);

// Load local settings if present
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Register tools
var tools = new List<IAgentTool>
{
    new ExploreDirectoryTool(),
    new ReadCodeFileTool(),
    new WriteCodeFileTool(),
    new RunTerminalCommandTool()
};
builder.Services.AddSingleton<IEnumerable<IAgentTool>>(tools);

// LLM service
string apiKey = builder.Configuration["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
string baseUrl = builder.Configuration["OPENAI_BASE_URL"] ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://generativelanguage.googleapis.com/v1beta/openai/";
string model = builder.Configuration["OPENAI_MODEL"] ?? Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gemini-1.5-flash";

ILlmService llmService;
if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_OPENAI_OR_GEMINI_API_KEY")
{
    llmService = new MockLlmService();
}
else
{
    llmService = new OpenAiLlmService(apiKey, baseUrl, model);
}
builder.Services.AddSingleton(llmService);

// Agent engine
builder.Services.AddTransient<AgentEngine>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
