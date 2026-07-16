using Microsoft.Extensions.Logging;
using NSubstitute;
using SampleDurable.Core.Models;
using SampleDurable.Core.Services;

namespace SampleDurable.Tests.Unit.Core;

[TestFixture]
public class StubResultPublisherTests
{
    [Test]
    public async Task Should_complete_and_log_one_information_entry()
    {
        var logger = Substitute.For<ILogger<StubResultPublisher>>();
        var publisher = new StubResultPublisher(logger);
        var summary = new AgentRunSummary(3, 2, 1);

        await publisher.Publish(summary, CancellationToken.None);

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
