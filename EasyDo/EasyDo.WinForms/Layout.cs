namespace EasyDo.WinForms;

public sealed partial class UiKit
{
    /// <summary>Строка из колонок с процентной шириной и отступом между ними.</summary>
    public TableLayoutPanel Columns(float[] percents, Control[] controls, int gap = 16)
    {
        ArgumentNullException.ThrowIfNull(percents);
        ArgumentNullException.ThrowIfNull(controls);
        if (controls.Length == 0 || controls.Length != percents.Length || controls.Any(c => c == null)
            || controls.Distinct().Count() != controls.Length || percents.Any(p => !float.IsFinite(p) || p <= 0))
            throw new ArgumentException("Each unique control needs one positive column weight.");
        ArgumentOutOfRangeException.ThrowIfNegative(gap);
        var t = new TableLayoutPanel
        {
            ColumnCount = controls.Length,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Bg
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

    public Panel Spacer(int height) => new() { Dock = DockStyle.Top, Height = height, BackColor = Bg };

    /// <summary>Панель инструментов: слева фильтры, справа кнопки.</summary>
    public Panel Toolbar(Control[] left, Control[] right, int height = 48)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = height, BackColor = Bg };
        var l = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, WrapContents = false, BackColor = Bg, Padding = new Padding(0, 4, 0, 0)
        };
        var r = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Bg,
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
    public Label ToolLabel(string text)
    {
        var l = Label(text, Body, Muted);
        l.Padding = new Padding(0, 6, 0, 0);
        return l;
    }

    public Label Footer()
    {
        return new Label
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Muted,
            Font = Small,
            BackColor = Card
        };
    }

}
