namespace SampleDurable.Tests.Analyzers;

internal static class DurableTestSources
{
    internal const string Stubs = @"
namespace Microsoft.Azure.Functions.Worker
{
    using System;
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class OrchestrationTriggerAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ActivityTriggerAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class FunctionAttribute : Attribute { public FunctionAttribute(string name) { } }
}

namespace Microsoft.DurableTask.Entities
{
    using System.Threading.Tasks;

    public readonly struct EntityInstanceId
    {
        public EntityInstanceId(string name, string key) { Name = name; Key = key; }
        public string Name { get; }
        public string Key { get; }
    }

    public abstract class TaskOrchestrationEntityFeature
    {
        public abstract Task SignalEntityAsync(EntityInstanceId id, string operationName, object input = null);
    }
}

namespace Microsoft.DurableTask
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.DurableTask.Entities;

    public readonly struct TaskName
    {
        public TaskName(string name) { Name = name; }
        public string Name { get; }
        public static implicit operator TaskName(string name) => new TaskName(name);
    }

    public class TaskOptions { }

    public abstract class TaskOrchestrationContext
    {
        public abstract string InstanceId { get; }
        public abstract DateTime CurrentUtcDateTime { get; }
        public abstract TaskOrchestrationEntityFeature Entities { get; }
        public abstract Task<T> CallActivityAsync<T>(TaskName name, object input = null, TaskOptions options = null);
        public abstract Task<T> CallSubOrchestratorAsync<T>(TaskName name, object input = null, TaskOptions options = null);
        public abstract Task<T> WaitForExternalEvent<T>(string eventName);
        public abstract Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken);
        public abstract Guid NewGuid();
    }

    public abstract class TaskOrchestrator<TInput, TOutput>
    {
        public abstract Task<TOutput> RunAsync(TaskOrchestrationContext context, TInput input);
    }
}

namespace SampleDurable.Testing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IAgentStreamClient
    {
        IAsyncEnumerable<string> StreamAsync(string prompt);
        IAsyncDisposable OpenSession(string prompt);
    }
}
";
}
