using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask.Client;
using NSubstitute;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Orchestrations;
using SampleDurable.Functions.Triggers;

namespace SampleDurable.Tests.Unit.Functions.Triggers;

[TestFixture]
public class CallbackTriggerTests
{
    private static HttpRequest CreateJsonRequest(AgentResult body)
    {
        var request = new DefaultHttpContext().Request;
        var json = JsonSerializer.Serialize(body, JsonSerializerOptions.Web);
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        request.ContentType = "application/json";
        request.ContentLength = request.Body.Length;
        return request;
    }

    [Test]
    public async Task Should_raise_the_agent_completed_event_on_the_named_instance()
    {
        var client = Substitute.For<DurableTaskClient>("test");
        var result = new AgentResult("item-1", true, "done");
        var request = CreateJsonRequest(result);

        var response = await CallbackTrigger.Run(request, client, "run-run-key-1-item-1");

        await client.Received(1).RaiseEventAsync(
            "run-run-key-1-item-1",
            AgentTaskOrchestrator.AgentCompletedEventName,
            Arg.Is<AgentResult>(r => r.WorkItemId == result.WorkItemId && r.Succeeded == result.Succeeded && r.Output == result.Output),
            Arg.Any<CancellationToken>());
        response.Should().BeOfType<AcceptedResult>();
    }
}
