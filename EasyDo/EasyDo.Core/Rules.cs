namespace EasyDo;

public sealed record RuleFailure(string Key, string Message);

public sealed class RuleException : InvalidOperationException
{
    public IReadOnlyList<RuleFailure> Failures { get; }
    public RuleException(IReadOnlyList<RuleFailure> failures)
        : base(string.Join(Environment.NewLine, failures.Select(f => f.Message)))
        => Failures = Array.AsReadOnly(failures.ToArray());
}

/// <summary>Immutable validation blocks. No UI, storage or application dependency.</summary>
public sealed class Rules<T>
{
    private readonly (string Key, Func<T, bool> Accept, string Message)[] _rules;
    private Rules((string, Func<T, bool>, string)[] rules) => _rules = rules;
    public static Rules<T> Create() => new([]);

    public Rules<T> Require(string key, Func<T, bool> accept, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(accept);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new([.. _rules, (key, accept, message)]);
    }

    public IReadOnlyList<RuleFailure> Validate(T value) => Array.AsReadOnly(_rules
        .Where(r => !r.Accept(value)).Select(r => new RuleFailure(r.Key, r.Message)).ToArray());

    public void Ensure(T value)
    {
        var failures = Validate(value);
        if (failures.Count > 0) throw new RuleException(failures);
    }
}
