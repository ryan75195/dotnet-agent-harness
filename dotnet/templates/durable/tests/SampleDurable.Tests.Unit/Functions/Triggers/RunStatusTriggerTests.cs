using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;
using NSubstitute;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Entities;
using SampleDurable.Functions.Triggers;

namespace SampleDurable.Tests.Unit.Functions.Triggers;

[TestFixture]
public class RunStatusTriggerTests
{
    [Test]
    public async Task Should_return_the_counter_state_when_the_entity_exists()
    {
        var client = Substitute.For<DurableTaskClient>("test");
        var entities = Substitute.For<DurableEntityClient>("test");
        client.Entities.Returns(entities);
        var entityId = new EntityInstanceId(nameof(RunCounterEntity), "run-run-key-1");
        var state = new RunCounterState(1, 0, 0);
        entities.GetEntityAsync<RunCounterState>(entityId)
            .Returns(new EntityMetadata<RunCounterState>(entityId, state));
        var request = new DefaultHttpContext().Request;

        var result = await RunStatusTrigger.Run(request, client, "run-run-key-1");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(state);
    }

    [Test]
    public async Task Should_return_not_found_when_the_entity_is_missing()
    {
        var client = Substitute.For<DurableTaskClient>("test");
        var entities = Substitute.For<DurableEntityClient>("test");
        client.Entities.Returns(entities);
        var entityId = new EntityInstanceId(nameof(RunCounterEntity), "run-missing");
        entities.GetEntityAsync<RunCounterState>(entityId)
            .Returns((EntityMetadata<RunCounterState>?)null);
        var request = new DefaultHttpContext().Request;

        var result = await RunStatusTrigger.Run(request, client, "run-missing");

        result.Should().BeOfType<NotFoundResult>();
    }
}
