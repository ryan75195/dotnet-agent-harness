using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Orchestrations;

namespace SampleDurable.Functions.Triggers;

public static class RunWebhookTrigger
{
    [Function(nameof(RunWebhookTrigger))]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "runs")] HttpRequest request,
        [DurableClient] DurableTaskClient client)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(client);

        var body = await request.ReadFromJsonAsync<AgentRunRequest>();
        ArgumentNullException.ThrowIfNull(body);

        var instanceId = $"run-{body.RunKey}";

        var existing = await client.GetInstanceAsync(instanceId);
        if (existing is not null)
        {
            return new OkObjectResult(new { instanceId, status = "already running" });
        }

        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(AgentRunOrchestrator),
            body,
            new StartOrchestrationOptions(instanceId));

        return new OkObjectResult(new { instanceId });
    }
}
