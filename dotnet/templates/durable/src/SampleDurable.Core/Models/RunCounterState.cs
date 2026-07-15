namespace SampleDurable.Core.Models;

public record RunCounterState(int Dispatched, int Completed, int TimedOut)
{
    public static RunCounterState Empty => new(0, 0, 0);
}
