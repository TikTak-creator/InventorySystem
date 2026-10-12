using System.Collections;
using System.Data;
using System.Data.Common;

namespace EasyDo.Core.Specs;

internal sealed class Probe
{
    public bool Opened, ConnectionDisposed, Committed, RolledBack, TransactionDisposed, CommandDisposed;
    public Exception OpenError, CommitError, RollbackError;
    public object Scalar = 42;
    public DataTable Table = new();
    public DbDataReader Reader;
    public DbCommand LastCommand;
    public IsolationLevel Isolation;
    public StoreFactory Factory => new(this);
    public sealed class StoreFactory(Probe probe)
    {
        public EasyDo.Data.Store Store => new(() => new Connection(probe));
    }
    public sealed class Connection(Probe probe) : DbConnection
    {
        private ConnectionState _state;
        public override string ConnectionString { get; set; }
        public override string Database => "probe";
        public override string DataSource => "probe";
        public override string ServerVersion => "1";
        public override ConnectionState State => _state;
        public override void Open() { if (probe.OpenError != null) throw probe.OpenError; probe.Opened = true; _state = ConnectionState.Open; }
        public override void Close() => _state = ConnectionState.Closed;
        public override void ChangeDatabase(string name) { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        { probe.Isolation = isolationLevel; return new Transaction(this, probe, isolationLevel); }
        protected override DbCommand CreateDbCommand() => probe.LastCommand = new Command(this, probe);
        protected override void Dispose(bool disposing) { probe.ConnectionDisposed = true; base.Dispose(disposing); }
    }
    private sealed class Transaction(DbConnection connection, Probe probe, IsolationLevel isolation) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => isolation;
        protected override DbConnection DbConnection => connection;
        public override void Commit() { if (probe.CommitError != null) throw probe.CommitError; probe.Committed = true; }
        public override void Rollback() { probe.RolledBack = true; if (probe.RollbackError != null) throw probe.RollbackError; }
        protected override void Dispose(bool disposing) { probe.TransactionDisposed = true; base.Dispose(disposing); }
    }
    private sealed class Command(DbConnection connection, Probe probe) : DbCommand
    {
        private readonly Parameters _parameters = new();
        public override string CommandText { get; set; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get; set; } = connection;
        protected override DbTransaction DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        public override void Cancel() { }
        public override int ExecuteNonQuery() => 1;
        public override object ExecuteScalar() => probe.Scalar;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new Parameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => probe.Reader = probe.Table.CreateDataReader();
        protected override void Dispose(bool disposing) { probe.CommandDisposed = true; base.Dispose(disposing); }
    }
    private sealed class Parameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; }
        public override string SourceColumn { get; set; }
        public override object Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }
    private sealed class Parameters : DbParameterCollection
    {
        private readonly List<DbParameter> _items = new();
        public override int Count => _items.Count;
        public override object SyncRoot => this;
        public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
        public override void AddRange(Array values) { foreach (var value in values) Add(value); }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string name) => _items.FindIndex(p => p.ParameterName == name);
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string name) => RemoveAt(IndexOf(name));
        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string name) => _items[IndexOf(name)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string name, DbParameter value) => _items[IndexOf(name)] = value;
    }
}
