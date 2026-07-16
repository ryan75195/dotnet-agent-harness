using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Entities;

namespace SampleDurable.Functions.Triggers;

public static class RunStatusTrigger
{
    [Function(nameof(RunStatusTrigger))]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "runs/{instanceId}/counters")] HttpRequest request,
        [DurableClient] DurableTaskClient client,
        string instanceId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(client);

        var entityId = new EntityInstanceId(nameof(RunCounterEntity), instanceId);
        var state = await client.Entities.GetEntityAsync<RunCounterState>(entityId);

        return state is null
            ? new NotFoundResult()
            : new OkObjectResult(state.State);
    }
}
