using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Activities;

namespace SampleDurable.Functions.Orchestrations;

public static class AgentRunOrchestrator
{
    public static string SubInstanceId(string runKey, string workItemId) => $"run-{runKey}-{workItemId}";

    [Function(nameof(AgentRunOrchestrator))]
    public static async Task<AgentRunSummary> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        AgentRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var tasks = new List<Task<AgentResult>>();
        foreach (var item in request.Items)
        {
            var options = new SubOrchestrationOptions(instanceId: SubInstanceId(request.RunKey, item.Id));
            tasks.Add(context.CallSubOrchestratorAsync<AgentResult>(nameof(AgentTaskOrchestrator), item, options));
        }

        var results = await Task.WhenAll(tasks);

        var summary = new AgentRunSummary(
            Total: results.Length,
            Succeeded: results.Count(r => r.Succeeded),
            TimedOut: results.Count(r => !r.Succeeded));

        await context.CallActivityAsync(nameof(PublishSummaryActivity), summary);

        return summary;
    }
}
