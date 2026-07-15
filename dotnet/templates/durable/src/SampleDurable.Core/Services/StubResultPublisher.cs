using Microsoft.Extensions.Logging;
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Models;

namespace SampleDurable.Core.Services;

public sealed class StubResultPublisher : IResultPublisher
{
    private readonly ILogger<StubResultPublisher> _logger;

    public StubResultPublisher(ILogger<StubResultPublisher> logger)
    {
        _logger = logger;
    }

    public Task Publish(AgentRunSummary summary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(summary);
        _logger.LogInformation(
            "Run summary: {Total} total, {Succeeded} succeeded, {TimedOut} timed out",
            summary.Total, summary.Succeeded, summary.TimedOut);
        return Task.CompletedTask;
    }
}
