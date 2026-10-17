using Inventory.Client;

namespace InventoryApp.Data;

internal static class ApiSession
{
    public static InventoryApi Current { get; set; }
    public static InventoryApi Require() => Current ?? throw new InvalidOperationException("Подключитесь к серверу.");
}
