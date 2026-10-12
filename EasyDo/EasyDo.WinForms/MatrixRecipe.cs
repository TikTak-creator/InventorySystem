namespace EasyDo.WinForms;

public readonly record struct Track
{
    internal SizeType Kind { get; }
    internal float Size { get; }
    private Track(SizeType kind, float size)
    {
        if (!float.IsFinite(size) || size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        Kind = kind; Size = size;
    }
    public static Track Percent(float weight) => new(SizeType.Percent, weight);
    public static Track Pixels(float size) => new(SizeType.Absolute, size);
}

/// <summary>Grid layout with validated spans; recipes do not mutate controls until Build.</summary>
public sealed class MatrixRecipe
{
    private readonly Track[] _columns, _rows;
    private readonly Color _background;
    private readonly List<(Control Control, int Column, int Row, int Columns, int Rows, Padding? Margin)> _cells = new();
    private bool _built;
    internal MatrixRecipe(Track[] columns, Track[] rows, Color background)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        if (columns.Length == 0 || rows.Length == 0 || columns.Concat(rows).Any(t => t.Size <= 0))
            throw new ArgumentException("A matrix needs positive row and column tracks.");
        _columns = columns.ToArray(); _rows = rows.ToArray(); _background = background;
    }
    public MatrixRecipe Cell(Control control, int column, int row, int columnSpan = 1, int rowSpan = 1, Padding? margin = null)
    {
        if (_built) throw new InvalidOperationException("This matrix has already been built.");
        ArgumentNullException.ThrowIfNull(control);
        if (column < 0 || row < 0 || columnSpan < 1 || rowSpan < 1
            || columnSpan > _columns.Length - column || rowSpan > _rows.Length - row)
            throw new ArgumentOutOfRangeException(nameof(column), "Cell span is outside the matrix.");
        if (_cells.Any(c => c.Control == control ||
            (column < c.Column + c.Columns && column + columnSpan > c.Column && row < c.Row + c.Rows && row + rowSpan > c.Row)))
            throw new ArgumentException("Cells must not overlap or reuse the same control.");
        _cells.Add((control, column, row, columnSpan, rowSpan, margin));
        return this;
    }
    public TableLayoutPanel Build()
    {
        if (_built) throw new InvalidOperationException("This matrix has already been built.");
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = _background, ColumnCount = _columns.Length, RowCount = _rows.Length };
        foreach (var track in _columns) panel.ColumnStyles.Add(new(track.Kind, track.Size));
        foreach (var track in _rows) panel.RowStyles.Add(new(track.Kind, track.Size));
        foreach (var cell in _cells)
        {
            cell.Control.Dock = DockStyle.Fill;
            if (cell.Margin.HasValue) cell.Control.Margin = cell.Margin.Value;
            panel.Controls.Add(cell.Control, cell.Column, cell.Row);
            if (cell.Columns > 1) panel.SetColumnSpan(cell.Control, cell.Columns);
            if (cell.Rows > 1) panel.SetRowSpan(cell.Control, cell.Rows);
        }
        _built = true;
        return panel;
    }
}

public sealed partial class UiKit
{
    public MatrixRecipe Matrix(float[] columns, float[] rows)
    {
        ArgumentNullException.ThrowIfNull(columns); ArgumentNullException.ThrowIfNull(rows);
        return Matrix(columns.Select(Track.Percent).ToArray(), rows.Select(Track.Percent).ToArray());
    }
    public MatrixRecipe Matrix(Track[] columns, Track[] rows) => new(columns, rows, Bg);
}
