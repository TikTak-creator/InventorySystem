using InventoryApp.Data;
using InventoryApp.UI;

namespace InventoryApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            DesktopApp.Run(AppStyle.Ui, "ru-RU", Database.EnsureCreated, () => new MainForm(), error =>
                MessageBox.Show("Не удалось подключиться к MySQL.\n\n" + error.Message +
                    "\n\nПроверьте, что сервер MySQL запущен, и строку подключения в файле appsettings.json.",
                    "StockFlow — ошибка подключения", MessageBoxButtons.OK, MessageBoxIcon.Error));
        }
        finally { AppStyle.Ui.Dispose(); }
    }
}
