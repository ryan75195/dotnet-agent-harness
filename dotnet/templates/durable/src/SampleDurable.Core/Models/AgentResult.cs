namespace SampleDurable.Core.Models;

public record AgentResult(string WorkItemId, bool Succeeded, string Output);
