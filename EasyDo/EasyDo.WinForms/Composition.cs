namespace EasyDo.WinForms;

/// <summary>Single-use layout recipe, preserving WinForms docking order.</summary>
public sealed class SurfaceRecipe
{
    private readonly Control _body;
    private readonly List<Control> _edges = new();
    private readonly Color _background;
    private bool _built;
    internal SurfaceRecipe(Control body, Color background)
    {
        ArgumentNullException.ThrowIfNull(body);
        _body = body;
        _background = background;
    }
    public SurfaceRecipe Above(Control control) => Edge(control, DockStyle.Top);
    public SurfaceRecipe Bottom(Control control) => Edge(control, DockStyle.Bottom);
    private SurfaceRecipe Edge(Control control, DockStyle dock)
    {
        if (_built) throw new InvalidOperationException("This layout has already been built.");
        ArgumentNullException.ThrowIfNull(control);
        if (control == _body || _edges.Contains(control)) throw new ArgumentException("A control can be added only once.");
        control.Dock = dock;
        _edges.Add(control);
        return this;
    }
    public Panel Build()
    {
        var panel = new Panel { BackColor = _background };
        Into(panel);
        return panel;
    }
    public void Into(Control host)
    {
        if (_built) throw new InvalidOperationException("This layout has already been built.");
        ArgumentNullException.ThrowIfNull(host);
        if (host == _body || _edges.Contains(host)) throw new ArgumentException("The host cannot be its own child.");
        host.SuspendLayout();
        try
        {
            _body.Dock = DockStyle.Fill;
            host.Controls.Add(_body);
            foreach (var edge in _edges) host.Controls.Add(edge);
            _body.BringToFront();
            _built = true;
        }
        finally { host.ResumeLayout(); }
    }
}

public sealed partial class UiKit
{
    public SurfaceRecipe Surface(Control body) => new(body, Card);
    public SurfaceRecipe Page(Control body) => new(body, Bg);

    public void Field(Control host, string label, Control input, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(input);
        var caption = Label(label, Small, Muted);
        caption.Location = new Point(x, y);
        input.Location = new Point(x, y + 22);
        host.Controls.Add(caption);
        host.Controls.Add(input);
    }
}
