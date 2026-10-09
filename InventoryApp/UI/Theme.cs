using System.Drawing.Drawing2D;

namespace InventoryApp.UI;

/// <summary>Тёмная тема и фабрика стилизованных контролов.</summary>
public static class Theme
{
    public static readonly Color Bg = Color.FromArgb(13, 15, 24);
    public static readonly Color Sidebar = Color.FromArgb(18, 20, 32);
    public static readonly Color Card = Color.FromArgb(24, 27, 42);
    public static readonly Color CardBorder = Color.FromArgb(42, 46, 68);
    public static readonly Color Input = Color.FromArgb(32, 36, 55);
    public static readonly Color Selection = Color.FromArgb(52, 54, 92);
    public static readonly Color Accent = Color.FromArgb(108, 99, 255);
    public static readonly Color Text = Color.FromArgb(232, 234, 242);
    public static readonly Color Muted = Color.FromArgb(138, 144, 168);
    public static readonly Color Success = Color.FromArgb(34, 197, 94);
    public static readonly Color Danger = Color.FromArgb(239, 68, 68);
    public static readonly Color Warning = Color.FromArgb(245, 158, 11);
    public static readonly Color Info = Color.FromArgb(56, 189, 248);
    public static readonly Color Pink = Color.FromArgb(236, 72, 153);

    public static readonly Font Body = new("Segoe UI", 10f);
    public static readonly Font Small = new("Segoe UI", 9f);
    public static readonly Font Bold = new("Segoe UI Semibold", 10f);
    public static readonly Font H1 = new("Segoe UI Semibold", 20f);
    public static readonly Font H2 = new("Segoe UI Semibold", 12f);
    public static readonly Font StatValue = new("Segoe UI Semibold", 20f);
    public static readonly Font Total = new("Segoe UI Semibold", 18f);

    public static string Money(decimal v) => v.ToString("N0") + " сом";

    public static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
        if (radius <= 0) { path.AddRectangle(r); return path; }
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ---------- Фабрика контролов ----------

    public static Button Btn(string text, Color? color = null, int width = 130)
    {
        var c = color ?? Accent;
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = c,
            ForeColor = Color.White,
            Font = Bold,
            Height = 36,
            Width = width,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Blend(c, Color.White, 0.12f);
        b.FlatAppearance.MouseDownBackColor = Blend(c, Color.Black, 0.15f);
        return b;
    }

    public static Button SecondaryBtn(string text, int width = 120)
    {
        var b = Btn(text, Input, width);
        b.ForeColor = Text;
        return b;
    }

    public static Label Label(string text, Font font = null, Color? color = null) => new()
    {
        Text = text,
        Font = font ?? Body,
        ForeColor = color ?? Text,
        AutoSize = true,
        BackColor = Color.Transparent
    };

    public static TextBox TextBox(string placeholder = "", int width = 240) => new()
    {
        PlaceholderText = placeholder,
        BackColor = Input,
        ForeColor = Text,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 11f),
        Width = width
    };

    public static ComboBox Combo(int width = 200, bool editable = false) => new()
    {
        DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = Input,
        ForeColor = Text,
        Font = new Font("Segoe UI", 11f),
        Width = width
    };

    public static NumericUpDown Number(decimal max = 10_000_000, int decimals = 0) => new()
    {
        Minimum = 0,
        Maximum = max,
        DecimalPlaces = decimals,
        ThousandsSeparator = true,
        BackColor = Input,
        ForeColor = Text,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 11f),
        Width = 200
    };

    public static DateTimePicker DatePicker(DateTime value) => new()
    {
        Format = DateTimePickerFormat.Short,
        Value = value,
        Width = 130,
        Font = new Font("Segoe UI", 10.5f),
        CalendarMonthBackground = Input,
        CalendarForeColor = Text
    };

    public static DataGridView Grid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Card,
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeight = 40,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = CardBorder,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoGenerateColumns = false,
            Font = Body
        };
        g.RowTemplate.Height = 38;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Card,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 9f),
            SelectionBackColor = Card,
            SelectionForeColor = Muted,
            Padding = new Padding(6, 0, 6, 0)
        };
        g.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Card,
            ForeColor = Text,
            SelectionBackColor = Selection,
            SelectionForeColor = Color.White,
            Padding = new Padding(6, 0, 6, 0)
        };
        return g;
    }

    public static DataGridViewTextBoxColumn Col(string prop, string header, float weight = 100,
        string format = null, bool right = false)
    {
        var c = new DataGridViewTextBoxColumn
        {
            Name = prop,
            DataPropertyName = prop,
            HeaderText = header,
            FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (format != null) c.DefaultCellStyle.Format = format;
        if (right)
        {
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        return c;
    }

    /// <summary>Заголовок страницы: крупный заголовок + подзаголовок.</summary>
    public static Panel PageHeader(string title, string subtitle)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Bg };
        var t = Label(title, H1);
        t.Location = new Point(0, 0);
        var s = Label(subtitle, Body, Muted);
        s.Location = new Point(3, 44);
        p.Controls.Add(t);
        p.Controls.Add(s);
        return p;
    }

    /// <summary>Карточка с заголовком и содержимым.</summary>
    public static Card TitledCard(string title, Control body, Control headerRight = null)
    {
        var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 14) };
        var head = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Card };
        var lbl = Label(title, H2);
        lbl.Location = new Point(0, 4);
        head.Controls.Add(lbl);
        if (headerRight != null)
        {
            headerRight.Dock = DockStyle.Right;
            head.Controls.Add(headerRight);
        }
        body.Dock = DockStyle.Fill;
        card.Controls.Add(body);
        card.Controls.Add(head);
        body.BringToFront();
        return card;
    }

    public static void StyleDialog(Form f, string title, Size size)
    {
        f.Text = title;
        f.ClientSize = size;
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.StartPosition = FormStartPosition.CenterParent;
        f.MaximizeBox = false;
        f.MinimizeBox = false;
        f.ShowInTaskbar = false;
        f.BackColor = Bg;
        f.ForeColor = Text;
        f.Font = Body;
    }

    /// <summary>Выполнить действие и показать ошибку, если что-то пошло не так.</summary>
    public static void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static bool Confirm(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public static void Notify(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, "StockFlow", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warn(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}

public interface IRefreshable
{
    void RefreshData();
}
