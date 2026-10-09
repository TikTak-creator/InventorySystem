using System.Globalization;
using InventoryApp.Data;
using InventoryApp.UI;

namespace InventoryApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var culture = new CultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

        try
        {
            Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Не удалось подключиться к MySQL.\n\n" + ex.Message +
                "\n\nПроверьте, что сервер MySQL запущен, и строку подключения в файле appsettings.json.",
                "StockFlow — ошибка подключения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm());
    }
}
