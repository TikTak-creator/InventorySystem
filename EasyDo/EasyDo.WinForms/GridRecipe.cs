using System.Linq.Expressions;
using System.Reflection;

namespace EasyDo.WinForms;

/// <summary>Typed columns: a renamed property produces a compiler error, not an empty UI cell.</summary>
public sealed class GridRecipe<T>
{
    private readonly DataGridView _grid;
    private readonly List<DataGridViewColumn> _columns = new();
    private bool _applied;
    internal GridRecipe(DataGridView grid) => _grid = grid;

    public GridRecipe<T> Column<TValue>(Expression<Func<T, TValue>> property, string title,
        float weight = 100, string format = null, bool right = false)
    {
        if (_applied) throw new InvalidOperationException("This grid recipe has already been applied.");
        ArgumentNullException.ThrowIfNull(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (!float.IsFinite(weight) || weight <= 0 || weight > 65535)
            throw new ArgumentOutOfRangeException(nameof(weight));
        if (property.Body is not MemberExpression { Member: PropertyInfo member } expression
            || expression.Expression != property.Parameters[0])
            throw new ArgumentException("Choose a direct property: row => row.Name.", nameof(property));
        if (_columns.Any(c => c.Name == member.Name) || _grid.Columns.Contains(member.Name))
            throw new ArgumentException($"Duplicate column: {member.Name}.", nameof(property));
        var column = new DataGridViewTextBoxColumn
        {
            Name = member.Name, DataPropertyName = member.Name, HeaderText = title,
            FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (format != null) column.DefaultCellStyle.Format = format;
        if (right)
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        _columns.Add(column);
        return this;
    }

    public DataGridView Apply()
    {
        if (_applied) throw new InvalidOperationException("This grid recipe has already been applied.");
        _grid.Columns.AddRange(_columns.ToArray());
        _applied = true;
        return _grid;
    }
}

public static class GridBlocks
{
    public static GridRecipe<T> Define<T>(this DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return new(grid);
    }
    public static T Selected<T>(this DataGridView grid) where T : class
        => grid.CurrentRow?.DataBoundItem as T;

    public static void BindRows<T, TKey>(this DataGridView grid, IEnumerable<T> rows,
        Func<T, TKey> key, int focusColumn = 0) where T : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(key);
        if (focusColumn < 0 || focusColumn >= grid.Columns.Count) throw new ArgumentOutOfRangeException(nameof(focusColumn));
        var previous = grid.Selected<T>();
        var previousKey = previous == null ? default : key(previous);
        grid.DataSource = rows.ToList();
        if (previous == null) return;
        foreach (DataGridViewRow row in grid.Rows)
            if (row.DataBoundItem is T item && EqualityComparer<TKey>.Default.Equals(key(item), previousKey))
            {
                row.Selected = true;
                grid.CurrentCell = row.Cells[focusColumn];
                break;
            }
    }
}
