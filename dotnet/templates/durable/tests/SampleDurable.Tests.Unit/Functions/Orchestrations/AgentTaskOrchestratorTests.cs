using FluentAssertions;
using SampleDurable.Functions.Orchestrations;

namespace SampleDurable.Tests.Unit.Functions.Orchestrations;

[TestFixture]
public class AgentTaskOrchestratorTests
{
    [Test]
    public void Should_expose_the_agent_completed_event_name_used_by_the_callback_trigger()
    {
        AgentTaskOrchestrator.AgentCompletedEventName.Should().Be("AgentCompleted");
    }

    [Test]
    public void Should_expose_a_two_hour_dispatch_timeout()
    {
        AgentTaskOrchestrator.DispatchTimeout.Should().Be(TimeSpan.FromHours(2));
    }
}
