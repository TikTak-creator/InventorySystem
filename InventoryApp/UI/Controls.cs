using System.Drawing.Drawing2D;

namespace InventoryApp.UI;

/// <summary>Панель-карточка со скруглёнными углами и рамкой.</summary>
public class Card : Panel
{
    public int Radius { get; set; } = 14;

    public Card()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Card;
        Padding = new Padding(16);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width > 0 && Height > 0)
        {
            using var path = Theme.RoundedRect(new Rectangle(0, 0, Width, Height), Radius);
            Region = new Region(path);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
        using var pen = new Pen(Theme.CardBorder);
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>Карточка показателя на главной странице.</summary>
public class StatCard : Card
{
    private readonly Label _value;
    private readonly Label _hint;
    private readonly Color _accent;

    public StatCard(string title, Color accent)
    {
        _accent = accent;
        Dock = DockStyle.Fill;

        var t = Theme.Label(title.ToUpperInvariant(), new Font("Segoe UI Semibold", 8.5f), Theme.Muted);
        t.Location = new Point(20, 16);
        _value = Theme.Label("—", Theme.StatValue);
        _value.Location = new Point(18, 36);
        _hint = Theme.Label("", Theme.Small, Theme.Muted);
        _hint.Location = new Point(20, 76);

        Controls.AddRange(new Control[] { t, _value, _hint });
    }

    public string Value { set => _value.Text = value; }
    public string Hint { set => _hint.Text = value; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Акцентная полоса слева
        using (var b = new SolidBrush(_accent))
        using (var p = Theme.RoundedRect(new Rectangle(0, 18, 4, Height - 36), 2))
            g.FillPath(b, p);

        // Иконка-кружок справа
        int size = 36;
        var circle = new Rectangle(Width - size - 18, 18, size, size);
        using (var b = new SolidBrush(Color.FromArgb(45, _accent)))
            g.FillEllipse(b, circle);
        using (var b = new SolidBrush(_accent))
            g.FillEllipse(b, Rectangle.Inflate(circle, -11, -11));
    }
}

/// <summary>Простой столбчатый график с градиентом.</summary>
public class BarChart : Control
{
    private List<(string Label, decimal Value)> _data = new();

    public BarChart()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Card;
        Font = Theme.Small;
    }

    public void SetData(IEnumerable<(string Label, decimal Value)> data)
    {
        _data = data.ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        if (_data.Count == 0 || Width < 40 || Height < 60)
        {
            TextRenderer.DrawText(g, "Нет данных", Font, ClientRectangle, Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var area = new Rectangle(8, 22, Width - 16, Height - 22 - 26);
        decimal max = Math.Max(1, _data.Max(d => d.Value));

        using (var pen = new Pen(Theme.CardBorder) { DashStyle = DashStyle.Dash })
            for (int i = 0; i <= 3; i++)
            {
                int y = area.Top + area.Height * i / 4;
                g.DrawLine(pen, area.Left, y, area.Right, y);
            }

        float slot = area.Width / (float)_data.Count;
        float bw = Math.Min(46f, slot * 0.55f);

        for (int i = 0; i < _data.Count; i++)
        {
            var (label, value) = _data[i];
            float h = (float)(value / max) * area.Height;
            float x = area.Left + slot * i + (slot - bw) / 2;
            bool isLast = i == _data.Count - 1;

            if (h >= 2)
            {
                var rect = new Rectangle((int)x, (int)(area.Bottom - h), (int)bw, (int)h);
                using var brush = new LinearGradientBrush(
                    new Rectangle(rect.X, rect.Y - 1, rect.Width, rect.Height + 2),
                    isLast ? Theme.Pink : Theme.Info, Theme.Accent, LinearGradientMode.Vertical);
                using var path = Theme.RoundedRect(rect, 6);
                g.FillPath(brush, path);
            }

            string vText = value >= 1000 ? (value / 1000m).ToString("0.#") + "k" : value.ToString("N0");
            var vRect = new Rectangle((int)(area.Left + slot * i), (int)(area.Bottom - h) - 20, (int)slot, 18);
            TextRenderer.DrawText(g, vText, Font, vRect, Theme.Text, TextFormatFlags.HorizontalCenter);

            var lRect = new Rectangle((int)(area.Left + slot * i), area.Bottom + 6, (int)slot, 18);
            TextRenderer.DrawText(g, label, Font, lRect, isLast ? Theme.Text : Theme.Muted, TextFormatFlags.HorizontalCenter);
        }
    }
}
