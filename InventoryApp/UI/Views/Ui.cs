namespace InventoryApp.UI.Views;

internal static class Ui
{
    /// <summary>Строка из колонок с процентной шириной и отступом между ними.</summary>
    public static TableLayoutPanel Columns(float[] percents, Control[] controls, int gap = 16)
    {
        var t = new TableLayoutPanel
        {
            ColumnCount = controls.Length,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Bg
        };
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < controls.Length; i++)
        {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, percents[i]));
            controls[i].Dock = DockStyle.Fill;
            controls[i].Margin = new Padding(0, 0, i < controls.Length - 1 ? gap : 0, 0);
            t.Controls.Add(controls[i], i, 0);
        }
        return t;
    }

    public static Panel Spacer(int height) => new() { Dock = DockStyle.Top, Height = height, BackColor = Theme.Bg };

    /// <summary>Панель инструментов: слева фильтры, справа кнопки.</summary>
    public static Panel Toolbar(Control[] left, Control[] right, int height = 48)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = height, BackColor = Theme.Bg };
        var l = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, WrapContents = false, BackColor = Theme.Bg, Padding = new Padding(0, 4, 0, 0)
        };
        var r = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Bg,
            FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 2, 0, 0)
        };
        foreach (var c in left) { c.Margin = new Padding(0, 0, 10, 0); l.Controls.Add(c); }
        foreach (var c in Enumerable.Reverse(right)) r.Controls.Add(c);
        p.Controls.Add(l);
        p.Controls.Add(r);
        l.BringToFront();
        return p;
    }

    /// <summary>Подпись, выровненная по центру строки тулбара.</summary>
    public static Label ToolLabel(string text)
    {
        var l = Theme.Label(text, Theme.Body, Theme.Muted);
        l.Padding = new Padding(0, 6, 0, 0);
        return l;
    }

    public static Label Footer()
    {
        return new Label
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Theme.Muted,
            Font = Theme.Small,
            BackColor = Theme.Card
        };
    }

    public static void ColorStatus(DataGridView grid, DataGridViewCellFormattingEventArgs e, string column = "Status")
    {
        if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name != column || e.Value is not string s) return;
        var c = s switch { "Нет в наличии" => Theme.Danger, "Мало" => Theme.Warning, _ => Theme.Success };
        e.CellStyle.ForeColor = c;
        e.CellStyle.SelectionForeColor = c;
        e.CellStyle.Font = Theme.Bold;
    }
}
