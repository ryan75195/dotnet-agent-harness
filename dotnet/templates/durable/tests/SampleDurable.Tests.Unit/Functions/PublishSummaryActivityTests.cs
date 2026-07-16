using NSubstitute;
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Activities;

namespace SampleDurable.Tests.Unit.Functions;

[TestFixture]
public class PublishSummaryActivityTests
{
    [Test]
    public async Task Should_forward_the_summary_to_the_publisher()
    {
        var publisher = Substitute.For<IResultPublisher>();
        var summary = new AgentRunSummary(3, 2, 1);

        var activity = new PublishSummaryActivity(publisher);

        await activity.Run(summary, CancellationToken.None);

        await publisher.Received(1).Publish(summary, Arg.Any<CancellationToken>());
    }
}
