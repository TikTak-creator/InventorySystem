using System.Data;
using System.Data.Common;
using System.Globalization;

namespace EasyDo.Data;

/// <summary>Owns connection/transaction lifetime. The factory must return a new connection per call.</summary>
public sealed class Store
{
    private readonly Func<DbConnection> _connect;
    private readonly int _timeout;
    public Store(Func<DbConnection> connect, int commandTimeout = 30)
    {
        ArgumentNullException.ThrowIfNull(connect);
        ArgumentOutOfRangeException.ThrowIfNegative(commandTimeout);
        _connect = connect;
        _timeout = commandTimeout;
    }

    public T Read<T>(Func<Session, T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var connection = _connect();
        connection.Open();
        using var session = new Session(connection, null, _timeout);
        return operation(session);
    }
    public void Run(Action<Session> operation) => Read(s => { operation(s); return true; });

    public T Transaction<T>(Func<Session, T> operation, IsolationLevel isolation = IsolationLevel.ReadCommitted)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var connection = _connect();
        connection.Open();
        using var transaction = connection.BeginTransaction(isolation);
        using var session = new Session(connection, transaction, _timeout);
        try
        {
            var result = operation(session);
            transaction.Commit();
            return result;
        }
        catch (Exception error)
        {
            // Keep the original error (including cancellation) if rollback also fails.
            try { transaction.Rollback(); }
            catch (Exception rollbackError) { error.Data["EasyDo.RollbackError"] = rollbackError; }
            throw;
        }
    }
    public void Transaction(Action<Session> operation, IsolationLevel isolation = IsolationLevel.ReadCommitted)
        => Transaction(s => { operation(s); return true; }, isolation);

    public async Task<T> ReadAsync<T>(Func<Session, CancellationToken, Task<T>> operation,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await using var connection = _connect();
        await connection.OpenAsync(cancellation).ConfigureAwait(false);
        using var session = new Session(connection, null, _timeout);
        return await operation(session, cancellation).ConfigureAwait(false);
    }

    public async Task<T> TransactionAsync<T>(Func<Session, CancellationToken, Task<T>> operation,
        CancellationToken cancellation = default, IsolationLevel isolation = IsolationLevel.ReadCommitted)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await using var connection = _connect();
        await connection.OpenAsync(cancellation).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(isolation, cancellation).ConfigureAwait(false);
        using var session = new Session(connection, transaction, _timeout);
        try
        {
            var result = await operation(session, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
            return result;
        }
        catch (Exception error)
        {
            try { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception rollbackError) { error.Data["EasyDo.RollbackError"] = rollbackError; }
            throw;
        }
    }
}

/// <summary>Short-lived, sequential session. Never cache it or return it from a Store callback.</summary>
public sealed class Session : IDisposable
{
    private readonly DbConnection _connection;
    private readonly DbTransaction? _transaction;
    private readonly int _timeout;
    private bool _disposed;
    internal Session(DbConnection connection, DbTransaction? transaction, int timeout)
        => (_connection, _transaction, _timeout) = (connection, transaction, timeout);
    private DbCommand Command(Sql sql)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(sql);
        return sql.Command(_connection, _transaction, _timeout);
    }
    public int Execute(Sql sql) { using var c = Command(sql); return c.ExecuteNonQuery(); }
    public T Scalar<T>(Sql sql)
    {
        using var c = Command(sql);
        return ConvertScalar<T>(c.ExecuteScalar());
    }
    public List<T> Query<T>(Sql sql, Func<DbDataReader, T> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        using var c = Command(sql);
        using var r = c.ExecuteReader();
        var result = new List<T>();
        while (r.Read()) result.Add(map(r));
        return result;
    }
    public async Task<int> ExecuteAsync(Sql sql, CancellationToken cancellation = default)
    {
        await using var c = Command(sql);
        return await c.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false);
    }
    public async Task<T> ScalarAsync<T>(Sql sql, CancellationToken cancellation = default)
    {
        await using var c = Command(sql);
        return ConvertScalar<T>(await c.ExecuteScalarAsync(cancellation).ConfigureAwait(false));
    }
    public async Task<List<T>> QueryAsync<T>(Sql sql, Func<DbDataReader, T> map, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        await using var c = Command(sql);
        await using var r = await c.ExecuteReaderAsync(cancellation).ConfigureAwait(false);
        var result = new List<T>();
        while (await r.ReadAsync(cancellation).ConfigureAwait(false)) result.Add(map(r));
        return result;
    }
    private static T ConvertScalar<T>(object? value)
    {
        if (value is null or DBNull)
        {
            if (default(T) is null) return default!;
            throw new InvalidOperationException("SQL returned NULL for a non-nullable scalar.");
        }
        if (value is T typed) return typed;
        return (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), CultureInfo.InvariantCulture);
    }
    public void Dispose() => _disposed = true;
}
