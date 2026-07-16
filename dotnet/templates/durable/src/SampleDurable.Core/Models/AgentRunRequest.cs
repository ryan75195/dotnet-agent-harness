namespace SampleDurable.Core.Models;

public record AgentRunRequest(string RunKey, IReadOnlyList<AgentWorkItem> Items);
