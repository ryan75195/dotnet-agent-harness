using Microsoft.Azure.Functions.Worker;
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Models;

namespace SampleDurable.Functions.Activities;

public sealed class PublishSummaryActivity
{
    private readonly IResultPublisher _publisher;

    public PublishSummaryActivity(IResultPublisher publisher)
    {
        _publisher = publisher;
    }

    [Function(nameof(PublishSummaryActivity))]
    public Task Run(
        [ActivityTrigger] AgentRunSummary summary,
        CancellationToken cancellationToken)
        => _publisher.Publish(summary, cancellationToken);
}
