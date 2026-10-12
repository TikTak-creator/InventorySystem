namespace EasyDo.WinForms;

/// <summary>Lazy pages kept alive between visits and disposed with their host.</summary>
public sealed class ViewDeck : IDisposable
{
    private readonly Control _host;
    private readonly Action<IRefreshable> _refresh;
    private readonly Dictionary<string, Func<UserControl>> _factories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UserControl> _pages = new(StringComparer.Ordinal);
    private bool _disposed;
    public ViewDeck(Control host, Action<IRefreshable> refresh = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.IsDisposed) throw new ObjectDisposedException(nameof(host));
        _host = host;
        _refresh = refresh ?? (page => page.RefreshData());
        host.Disposed += HostDisposed;
    }
    private void HostDisposed(object sender, EventArgs args) => Dispose();
    public void Register(string key, Func<UserControl> factory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        if (!_factories.TryAdd(key, factory)) throw new ArgumentException($"Duplicate page: {key}.", nameof(key));
    }
    public UserControl Show(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_factories.TryGetValue(key, out var factory)) throw new KeyNotFoundException($"Unknown page: {key}.");
        if (!_pages.TryGetValue(key, out var page))
        {
            page = factory() ?? throw new InvalidOperationException("Page factory returned null.");
            if (page.IsDisposed || _pages.Values.Contains(page)) throw new InvalidOperationException("Page factory must return a new live control.");
            _pages.Add(key, page);
        }
        _host.SuspendLayout();
        try
        {
            _host.Controls.Clear();
            page.Dock = DockStyle.Fill;
            _host.Controls.Add(page);
        }
        finally { _host.ResumeLayout(); }
        if (page is IRefreshable refreshable) _refresh(refreshable);
        return page;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _host.Disposed -= HostDisposed;
        foreach (var page in _pages.Values) page.Dispose();
        _pages.Clear();
        _factories.Clear();
    }
}
