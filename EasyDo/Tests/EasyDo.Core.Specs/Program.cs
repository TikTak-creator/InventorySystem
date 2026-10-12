using System.Data;
using EasyDo;
using EasyDo.Data;
using EasyDo.Core.Specs;

var count = 0;
void Test(string name, Action run) { run(); Console.WriteLine("PASS " + name); count++; }
void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
T Throws<T>(Action run) where T : Exception
{
    try { run(); } catch (T error) { return error; }
    throw new Exception("Expected " + typeof(T).Name);
}

Test("flow branches do not modify their source recipe", () =>
{
    var recipe = Flow<List<int>>.Start().Then(s => s.Add(1));
    var branch = recipe.Then(s => s.Add(2));
    Assert(recipe.Run(new()).SequenceEqual([1]));
    Assert(branch.Run(new()).SequenceEqual([1, 2]));
});
Test("flow failure prevents later side effects", () =>
{
    var touched = false;
    Throws<InvalidOperationException>(() => Flow<int>.Start().Then(_ => throw new InvalidOperationException())
        .Then(_ => touched = true).Run(0));
    Assert(!touched);
});
Test("conditional steps only run when accepted", () =>
{
    var calls = 0;
    var recipe = Flow<int>.Start().When(n => n > 0, _ => calls++);
    recipe.Run(0); recipe.Run(1); Assert(calls == 1);
});
Test("rules accumulate failures and preserve keys", () =>
{
    var rules = Rules<int>.Create().Require("positive", n => n > 0, "positive required")
        .Require("even", n => n % 2 == 0, "even required");
    var failures = Throws<RuleException>(() => rules.Ensure(-1)).Failures;
    Assert(failures.Count == 2 && failures[0].Key == "positive");
});
Test("rules branch independently", () =>
{
    var rules = Rules<int>.Create().Require("positive", n => n > 0, "positive required");
    var strict = rules.Require("large", n => n > 10, "large required");
    Assert(rules.Validate(1).Count == 0 && strict.Validate(1).Count == 1);
});
Test("a failed check stops the flow", () =>
{
    var touched = false;
    Throws<RuleException>(() => Flow<int>.Start().Check(Rules<int>.Create().Require("x", n => n > 0, "bad"))
        .Then(_ => touched = true).Run(0)); Assert(!touched);
});
Test("async flow respects pre-cancellation", () =>
{
    var touched = false;
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    Throws<OperationCanceledException>(() => AsyncFlow<int>.Start().Then((_, _) => { touched = true; return Task.CompletedTask; })
        .RunAsync(0, cancel.Token).GetAwaiter().GetResult()); Assert(!touched);
});
Test("async flow cancellation between steps", () =>
{
    var touched = false;
    using var cancel = new CancellationTokenSource();
    Throws<OperationCanceledException>(() => AsyncFlow<int>.Start().Then((_, _) => { cancel.Cancel(); return Task.CompletedTask; })
        .Then((_, _) => { touched = true; return Task.CompletedTask; }).RunAsync(0, cancel.Token).GetAwaiter().GetResult());
    Assert(!touched);
});
Test("async recipe runs in order", () =>
{
    var list = AsyncFlow<List<int>>.Start().Then(async (s, ct) => { await Task.Yield(); s.Add(1); })
        .Then((s, ct) => { s.Add(2); return Task.CompletedTask; }).RunAsync(new()).GetAwaiter().GetResult();
    Assert(list.SequenceEqual([1, 2]));
});
Test("parameters are values, not SQL text", () =>
{
    var probe = new Probe(); var attack = "'; DROP TABLE products; --";
    probe.Factory.Store.Run(s => s.Execute(Sql.TextOnly("SELECT @name").Bind("name", attack)));
    Assert(probe.LastCommand.CommandText == "SELECT @name" && (string)probe.LastCommand.Parameters[0].Value == attack);
});
Test("null parameters are DBNull with explicit type", () =>
{
    var probe = new Probe(); probe.Factory.Store.Run(s => s.Execute(Sql.TextOnly("SELECT @name").Bind("name", null, DbType.String)));
    Assert(probe.LastCommand.Parameters[0].Value == DBNull.Value && probe.LastCommand.Parameters[0].DbType == DbType.String);
});
Test("SQL binding is immutable", () =>
{
    var probe = new Probe(); var sql = Sql.TextOnly("SELECT 1"); var bound = sql.Bind("a", 1);
    probe.Factory.Store.Run(s => s.Execute(sql)); Assert(probe.LastCommand.Parameters.Count == 0);
    probe.Factory.Store.Run(s => s.Execute(bound)); Assert(probe.LastCommand.Parameters.Count == 1);
});
Test("duplicate bindings and blank SQL are rejected", () =>
{
    Throws<ArgumentException>(() => Sql.TextOnly(" "));
    Throws<ArgumentException>(() => Sql.TextOnly("SELECT @x").Bind("x", 1).Bind("@x", 2));
});
Test("connection and command disposed after read", () =>
{
    var probe = new Probe(); Assert(probe.Factory.Store.Read(s => s.Scalar<int>(Sql.TextOnly("SELECT 42"))) == 42);
    Assert(probe.Opened && probe.ConnectionDisposed && probe.CommandDisposed);
});
Test("failed connection open still disposes the connection", () =>
{
    var probe = new Probe { OpenError = new InvalidOperationException("open") };
    Assert(ReferenceEquals(Throws<InvalidOperationException>(() => probe.Factory.Store.Run(_ => { })), probe.OpenError));
    Assert(probe.ConnectionDisposed);
});
Test("transaction commits and attaches every command", () =>
{
    var probe = new Probe();
    var result = probe.Factory.Store.Transaction(s => { s.Execute(Sql.TextOnly("UPDATE x")); Assert(probe.LastCommand.Transaction != null); return 7; });
    Assert(result == 7 && probe.Committed && !probe.RolledBack && probe.TransactionDisposed && probe.ConnectionDisposed);
});
Test("explicit isolation reaches the provider", () =>
{
    var probe = new Probe(); probe.Factory.Store.Transaction(_ => { }, IsolationLevel.Serializable);
    Assert(probe.Isolation == IsolationLevel.Serializable);
});
Test("transaction failure rolls back with original error", () =>
{
    var probe = new Probe(); var original = new InvalidOperationException("business failure");
    Assert(ReferenceEquals(Throws<InvalidOperationException>(() => probe.Factory.Store.Transaction(_ => throw original)), original));
    Assert(probe.RolledBack && !probe.Committed && probe.TransactionDisposed && probe.ConnectionDisposed);
});
Test("rollback failure does not conceal the original error", () =>
{
    var probe = new Probe { RollbackError = new Exception("rollback") }; var original = new InvalidOperationException("business");
    var error = Throws<InvalidOperationException>(() => probe.Factory.Store.Transaction(_ => throw original));
    Assert(ReferenceEquals(error, original) && ReferenceEquals(error.Data["EasyDo.RollbackError"], probe.RollbackError));
});
Test("commit failure is propagated and cleanup attempted", () =>
{
    var probe = new Probe { CommitError = new InvalidOperationException("commit") };
    Assert(ReferenceEquals(Throws<InvalidOperationException>(() => probe.Factory.Store.Transaction(_ => 1)), probe.CommitError));
    Assert(probe.RolledBack && probe.TransactionDisposed && probe.ConnectionDisposed);
});
Test("escaped sessions cannot issue commands", () =>
{
    var probe = new Probe(); Session escaped = null; probe.Factory.Store.Run(s => escaped = s);
    Throws<ObjectDisposedException>(() => escaped.Execute(Sql.TextOnly("UPDATE x")));
});
Test("query eagerly materializes rows and disposes reader", () =>
{
    var probe = new Probe(); probe.Table.Columns.Add("n", typeof(int)); probe.Table.Rows.Add(4); probe.Table.Rows.Add(5);
    Assert(probe.Factory.Store.Read(s => s.Query(Sql.TextOnly("SELECT n"), r => r.GetInt32(0))).SequenceEqual([4, 5]));
    Assert(probe.Reader.IsClosed && probe.CommandDisposed);
});
Test("mapping failure closes reader and connection", () =>
{
    var probe = new Probe(); probe.Table.Columns.Add("n", typeof(int)); probe.Table.Rows.Add(4);
    Throws<InvalidOperationException>(() => probe.Factory.Store.Read(s => s.Query<int>(Sql.TextOnly("SELECT n"), _ => throw new InvalidOperationException())));
    Assert(probe.Reader.IsClosed && probe.ConnectionDisposed && probe.CommandDisposed);
});
Test("NULL scalar is explicit for reference and value types", () =>
{
    var probe = new Probe { Scalar = DBNull.Value };
    Assert(probe.Factory.Store.Read(s => s.Scalar<string>(Sql.TextOnly("SELECT NULL"))) == null);
    Throws<InvalidOperationException>(() => probe.Factory.Store.Read(s => s.Scalar<int>(Sql.TextOnly("SELECT NULL"))));
});
Test("nullable scalar conversion handles numeric provider types", () =>
{
    var probe = new Probe { Scalar = 9L };
    Assert(probe.Factory.Store.Read(s => s.Scalar<int?>(Sql.TextOnly("SELECT 9"))) == 9);
});
Test("async read uses same parameter and lifetime contracts", () =>
{
    var probe = new Probe();
    Assert(probe.Factory.Store.ReadAsync((s, ct) => s.ScalarAsync<int>(Sql.TextOnly("SELECT @x").Bind("x", 42), ct)).GetAwaiter().GetResult() == 42);
    Assert(probe.ConnectionDisposed && probe.CommandDisposed);
});
Test("async transaction cancellation rolls back", () =>
{
    var probe = new Probe(); using var cancel = new CancellationTokenSource();
    Throws<OperationCanceledException>(() => probe.Factory.Store.TransactionAsync((s, ct) =>
    { cancel.Cancel(); return Task.FromResult(1); }, cancel.Token).GetAwaiter().GetResult());
    Assert(probe.RolledBack && !probe.Committed && probe.ConnectionDisposed);
});
Test("async transaction preserves rollback failure detail", () =>
{
    var probe = new Probe { RollbackError = new Exception("rollback") }; var original = new InvalidOperationException("business");
    var error = Throws<InvalidOperationException>(() => probe.Factory.Store.TransactionAsync<int>((_, _) => throw original).GetAwaiter().GetResult());
    Assert(ReferenceEquals(error, original) && error.Data.Contains("EasyDo.RollbackError"));
});
Test("async query materializes and disposes", () =>
{
    var probe = new Probe(); probe.Table.Columns.Add("n", typeof(int)); probe.Table.Rows.Add(3);
    var rows = probe.Factory.Store.ReadAsync((s, ct) => s.QueryAsync(Sql.TextOnly("SELECT n"), r => r.GetInt32(0), ct)).GetAwaiter().GetResult();
    Assert(rows.Single() == 3 && probe.Reader.IsClosed && probe.ConnectionDisposed);
});
Console.WriteLine($"{count} core checks passed.");
