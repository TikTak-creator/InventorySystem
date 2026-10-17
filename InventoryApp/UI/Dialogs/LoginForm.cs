using System.Text.Json;
using Inventory.Client;
using InventoryApp.Data;

namespace InventoryApp.UI.Dialogs;

public sealed class LoginForm : Form
{
    private readonly TextBox _server = AppStyle.Ui.TextBox("https://localhost:7071", 430);
    private readonly TextBox _username = AppStyle.Ui.TextBox("Логин", 430);
    private readonly TextBox _password = AppStyle.Ui.TextBox("Пароль", 430);
    private readonly Label _error = new() { AutoSize = false, ForeColor = AppStyle.Ui.Danger, Size = new Size(430, 65) };
    private readonly CancellationTokenSource _cancel = new();
    private readonly Button _login;
    private bool _busy;
    private bool _disposed;
    public InventoryApi Session { get; private set; }
    public LoginForm()
    {
        AppStyle.Ui.StyleDialog(this, "StockFlow — вход на сервер", new Size(480, 430));
        var title = AppStyle.Ui.Label("Вход в StockFlow", AppStyle.Ui.H2); title.Location = new Point(24, 20); Controls.Add(title);
        var hint = AppStyle.Ui.Label("Укажите сервер и свою учётную запись", AppStyle.Ui.Small, AppStyle.Ui.Muted);
        hint.Location = new Point(24, 49); Controls.Add(hint);
        AppStyle.Ui.Field(this, "Адрес сервера (HTTPS)", _server, 24, 85);
        AppStyle.Ui.Field(this, "Логин", _username, 24, 150);
        AppStyle.Ui.Field(this, "Пароль", _password, 24, 215);
        _password.UseSystemPasswordChar = true;
        _error.Location = new Point(24, 280); Controls.Add(_error);
        _login = AppStyle.Ui.Btn("Войти", AppStyle.Ui.Accent, 150); _login.Location = new Point(306, 365);
        _login.Click += async (_, _) => await Login(); Controls.Add(_login); AcceptButton = _login;
        var cancel = AppStyle.Ui.SecondaryBtn("Отмена", 110); cancel.Location = new Point(186, 365); cancel.DialogResult = DialogResult.Cancel;
        Controls.Add(cancel); CancelButton = cancel;
        var config = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(config))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(config));
            if (json.RootElement.TryGetProperty("Server", out var server) && server.TryGetProperty("BaseUrl", out var url)) _server.Text = url.GetString();
        }
        Shown += (_, _) => _username.Focus();
        FormClosing += (_, _) => _cancel.Cancel();
    }
    private async Task Login()
    {
        if (_busy) return;
        _busy = true; _login.Enabled = false; _error.Text = "";
        InventoryApi candidate = null;
        try
        {
            candidate = new InventoryApi(_server.Text.Trim());
            await candidate.LoginAsync(_username.Text.Trim(), _password.Text, _cancel.Token);
            if (IsDisposed || _cancel.IsCancellationRequested) return;
            Session = candidate; candidate = null;
            _password.Clear(); DialogResult = DialogResult.OK;
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!IsDisposed) _error.Text = error is HttpRequestException
                ? "Не удалось подключиться. Проверьте адрес, запуск сервера и доверие его TLS-сертификату." : error.Message;
        }
        finally { candidate?.Dispose(); _busy = false; if (!IsDisposed) _login.Enabled = true; }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed) { _disposed = true; _cancel.Cancel(); _cancel.Dispose(); }
        base.Dispose(disposing);
    }
}
