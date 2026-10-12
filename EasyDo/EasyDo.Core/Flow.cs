namespace EasyDo;

/// <summary>An immutable recipe. Each run receives its own state; recipes can be reused.</summary>
public sealed class Flow<T>
{
    private readonly Action<T>[] _steps;
    private Flow(Action<T>[] steps) => _steps = steps;
    public static Flow<T> Start() => new([]);

    public Flow<T> Then(Action<T> step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new([.. _steps, step]);
    }

    public Flow<T> Check(Rules<T> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return Then(rules.Ensure);
    }

    public Flow<T> When(Func<T, bool> condition, Action<T> step)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(step);
        return Then(state => { if (condition(state)) step(state); });
    }

    public T Run(T state)
    {
        foreach (var step in _steps) step(state);
        return state;
    }
}

/// <summary>Async recipe with cancellation between steps and inside each operation.</summary>
public sealed class AsyncFlow<T>
{
    private readonly Func<T, CancellationToken, Task>[] _steps;
    private AsyncFlow(Func<T, CancellationToken, Task>[] steps) => _steps = steps;
    public static AsyncFlow<T> Start() => new([]);
    public AsyncFlow<T> Then(Func<T, CancellationToken, Task> step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new([.. _steps, step]);
    }
    public async Task<T> RunAsync(T state, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        foreach (var step in _steps)
        {
            cancellation.ThrowIfCancellationRequested();
            await step(state, cancellation).ConfigureAwait(false);
        }
        return state;
    }
}
