using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using FluentAssertions.Execution;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Orchestrations;

namespace SampleDurable.Tests.Integration;

[TestFixture]
public class OrchestrationLifecycleTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private HttpClient _client = null!;

    [SetUp]
    public void SetUp() => _client = new HttpClient { BaseAddress = FunctionHostFixture.BaseAddress };

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    public async Task Should_complete_the_run_when_every_work_item_calls_back()
    {
        var runKey = $"key-{Guid.NewGuid():N}";
        var request = new AgentRunRequest(runKey, [new AgentWorkItem("item-1", "do it")]);

        var start = await _client.PostAsJsonAsync("runs", request);
        start.IsSuccessStatusCode.Should().BeTrue();

        var subInstanceId = AgentRunOrchestrator.SubInstanceId(runKey, "item-1");
        await WaitForStatus(subInstanceId, "Running");

        var callback = await _client.PostAsJsonAsync(
            $"runs/{subInstanceId}/callback",
            new AgentResult("item-1", true, "done"));
        callback.IsSuccessStatusCode.Should().BeTrue();

        var status = await WaitForStatus($"run-{runKey}", "Completed");
        var summary = status.Output.Deserialize<AgentRunSummary>(JsonOptions);

        using (new AssertionScope())
        {
            summary!.Total.Should().Be(1);
            summary.Succeeded.Should().Be(1);
            summary.TimedOut.Should().Be(0);
        }
    }

    [Test]
    public async Task Should_return_the_existing_run_for_a_duplicate_webhook_delivery()
    {
        var runKey = $"key-{Guid.NewGuid():N}";
        var request = new AgentRunRequest(runKey, [new AgentWorkItem("item-1", "do it")]);

        var first = await _client.PostAsJsonAsync("runs", request);
        var second = await _client.PostAsJsonAsync("runs", request);

        using (new AssertionScope())
        {
            first.IsSuccessStatusCode.Should().BeTrue();
            second.IsSuccessStatusCode.Should().BeTrue();
            (await second.Content.ReadAsStringAsync()).Should().Contain("already running");
        }
    }

    private sealed record InstanceStatus(
        [property: JsonPropertyName("runtimeStatus")] string RuntimeStatus,
        [property: JsonPropertyName("output")] JsonElement Output);

    private async Task<InstanceStatus> WaitForStatus(string instanceId, string expected)
    {
        var uri = new Uri(
            FunctionHostFixture.BaseAddress,
            $"../runtime/webhooks/durabletask/instances/{instanceId}");

        var deadline = DateTime.UtcNow.AddSeconds(60);
        var last = "(never responded)";

        while (DateTime.UtcNow < deadline)
        {
            var response = await _client.GetAsync(uri);
            if (response.IsSuccessStatusCode)
            {
                var status = await response.Content.ReadFromJsonAsync<InstanceStatus>(JsonOptions);
                if (status is not null)
                {
                    last = status.RuntimeStatus;
                    if (string.Equals(last, expected, StringComparison.Ordinal))
                    {
                        return status;
                    }

                    if (last is "Failed" or "Terminated")
                    {
                        Assert.Fail($"Instance {instanceId} reached terminal status {last}, expected {expected}.");
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail($"Instance {instanceId} did not reach {expected} within 60s. Last status: {last}.");
        return null!;
    }
}
