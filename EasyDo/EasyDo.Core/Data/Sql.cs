using System.Data;
using System.Data.Common;

namespace EasyDo.Data;

/// <summary>SQL stays explicit. Values are always bound through provider parameters.</summary>
public sealed class Sql
{
    private readonly (string Name, object? Value, DbType? Type)[] _parameters;
    public string Text { get; }
    private Sql(string text, (string, object?, DbType?)[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
        _parameters = parameters;
    }
    public static Sql TextOnly(string text) => new(text, []);
    public Sql Bind(string name, object? value, DbType? type = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!name.StartsWith('@')) name = "@" + name;
        if (_parameters.Any(p => p.Name == name))
            throw new ArgumentException($"Parameter {name} is already bound.", nameof(name));
        return new(Text, [.. _parameters, (name, value, type)]);
    }
    internal DbCommand Command(DbConnection connection, DbTransaction? transaction, int timeout)
    {
        var command = connection.CreateCommand();
        try
        {
            command.CommandText = Text;
            command.CommandTimeout = timeout;
            command.Transaction = transaction;
            foreach (var (name, value, type) in _parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value ?? DBNull.Value;
                if (type.HasValue) parameter.DbType = type.Value;
                command.Parameters.Add(parameter);
            }
            return command;
        }
        catch { command.Dispose(); throw; }
    }
}
