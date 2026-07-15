using FluentAssertions;
using SampleDurable.Functions.Orchestrations;

namespace SampleDurable.Tests.Unit.Functions.Orchestrations;

[TestFixture]
public class AgentRunOrchestratorTests
{
    [Test]
    public void Should_derive_the_sub_instance_id_from_the_run_key_and_work_item_id()
    {
        var instanceId = AgentRunOrchestrator.SubInstanceId("run-key-1", "item-1");

        instanceId.Should().Be("run-run-key-1-item-1");
    }

    [Test]
    public void Should_derive_distinct_sub_instance_ids_for_distinct_work_items()
    {
        var first = AgentRunOrchestrator.SubInstanceId("run-key-1", "item-1");
        var second = AgentRunOrchestrator.SubInstanceId("run-key-1", "item-2");

        first.Should().NotBe(second);
    }
}
