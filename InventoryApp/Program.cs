using InventoryApp.Data;
using InventoryApp.UI;
using InventoryApp.UI.Dialogs;

namespace InventoryApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            DesktopApp.Run(AppStyle.Ui, "ru-RU", () =>
            {
                using var login = new LoginForm();
                if (login.ShowDialog() != DialogResult.OK) throw new OperationCanceledException();
                ApiSession.Current = login.Session;
            }, () => new MainForm(), error =>
            {
                if (error is not OperationCanceledException) AppStyle.Ui.Warn(null, error.Message);
            });
        }
        finally
        {
            if (ApiSession.Current is { } session)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { session.LogoutAsync(timeout.Token).GetAwaiter().GetResult(); } catch (Exception) { }
                session.Dispose(); ApiSession.Current = null;
            }
            AppStyle.Ui.Dispose();
        }
    }
}
