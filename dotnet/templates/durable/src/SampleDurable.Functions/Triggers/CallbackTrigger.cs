using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Orchestrations;

namespace SampleDurable.Functions.Triggers;

public static class CallbackTrigger
{
    [Function(nameof(CallbackTrigger))]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "runs/{instanceId}/callback")] HttpRequest request,
        [DurableClient] DurableTaskClient client,
        string instanceId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(client);

        var result = await request.ReadFromJsonAsync<AgentResult>();
        ArgumentNullException.ThrowIfNull(result);

        await client.RaiseEventAsync(instanceId, AgentTaskOrchestrator.AgentCompletedEventName, result);

        return new AcceptedResult();
    }
}
