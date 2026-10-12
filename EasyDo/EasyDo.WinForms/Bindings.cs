namespace EasyDo.WinForms;

/// <summary>Explicit form binding: no reflection and no surprise two-way model mutation.</summary>
public sealed class FormBinding<T>
{
    private readonly List<(Action<T> Load, Action<T> Save)> _fields = new();
    public FormBinding<T> Text(TextBox control, Func<T, string> read, Action<T, string> write)
        => Bind(control, read, write, () => control.Text, value => control.Text = value ?? "");
    public FormBinding<T> Choice(ComboBox control, Func<T, string> read, Action<T, string> write)
        => Bind(control, read, write, () => control.Text, value => control.Text = value ?? "");
    public FormBinding<T> Number(NumericUpDown control, Func<T, decimal> read, Action<T, decimal> write)
        => Bind(control, read, write, () => control.Value, value => control.Value = value);
    public FormBinding<T> Bind<TValue>(Control control, Func<T, TValue> read, Action<T, TValue> write,
        Func<TValue> get, Action<TValue> set)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        _fields.Add((model => set(read(model)), model => write(model, get())));
        return this;
    }
    public void Load(T model) { foreach (var field in _fields) field.Load(model); }
    public T Save(T model) { foreach (var field in _fields) field.Save(model); return model; }
}

/// <summary>Debounce tied to its owning control's lifetime.</summary>
public sealed class DebouncedAction : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Control _owner;
    private readonly EventHandler _onDisposed;
    private bool _disposed;
    public DebouncedAction(Control owner, Action action, int milliseconds = 300)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfLessThan(milliseconds, 1);
        if (owner.IsDisposed) throw new ObjectDisposedException(nameof(owner));
        _owner = owner;
        _timer = new() { Interval = milliseconds };
        _timer.Tick += (_, _) => { _timer.Stop(); action(); };
        _onDisposed = (_, _) => Dispose();
        owner.Disposed += _onDisposed;
    }
    public void Trigger()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer.Stop();
        _timer.Start();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _owner.Disposed -= _onDisposed;
        _timer.Dispose();
    }
}
