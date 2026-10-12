using System.Text.Json;
using EasyDo.Data;
using InventoryApp.Data;
using InventoryApp.Models;
using MySqlConnector;

namespace StockFlow.Specs;

internal static class MySqlSpecs
{
    internal static void Run(string settingsPath)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var builder = new MySqlConnectionStringBuilder(settings.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString());
        var testDatabase = "easydo_test_" + Guid.NewGuid().ToString("N");
        builder.Database = "";
        builder.ConnectionTimeout = 5;
        var serverConnection = builder.ConnectionString;
        var server = new Store(() => new MySqlConnection(serverConnection));
        // This is the only database this test runner creates or deletes. Never use inventory_db.
        server.Run(s => s.Execute(Sql.TextOnly($"CREATE DATABASE `{testDatabase}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci")));
        try
        {
            builder.Database = testDatabase;
            var connection = builder.ConnectionString;
            var store = new Store(() => new MySqlConnection(connection));
            var products = new ProductRepository(store);
            var sales = new SaleRepository(store);
            void Schema() => store.Run(s => { foreach (var ddl in Database.Schema) s.Execute(Sql.TextOnly(ddl)); });
            Product Add(string sku, int quantity = 10, decimal price = 20, decimal cost = 12) => new()
            { Sku = sku, Name = "Товар " + sku, Category = "Тест", Quantity = quantity, Price = price, PurchasePrice = cost, MinQuantity = 2 };
            Product Persist(Product p) { p.Id = products.Insert(p); return p; }
            Product Reload(int id) => products.GetAll().Single(p => p.Id == id);
            int SalesCount() => store.Read(s => s.Scalar<int>(Sql.TextOnly("SELECT COUNT(*) FROM sales")));

            Program.Test("MySQL schema can be initialized twice and empty statistics are zero", () =>
            { Schema(); Schema(); var stats = sales.GetStats(); Program.Assert(stats.ProductsCount == 0 && stats.TodayRevenue == 0); });
            var milk = Persist(Add("MILK", price: 20.50m));
            Program.Test("MySQL product CRUD, categories, search and decimal prices", () =>
            {
                var p = Reload(milk.Id); Program.Assert(p.Price == 20.50m && p.Quantity == 10);
                Program.Assert(products.GetAll("MILK", "Тест").Single().Id == milk.Id && products.GetCategories().Contains("Тест"));
                Program.Assert(products.SkuExists("MILK", 0) && !products.SkuExists("MILK", milk.Id));
            });
            Program.Test("MySQL same-price update makes no price history", () =>
            { products.Update(milk); Program.Assert(products.GetPriceHistory(milk.Id).Count == 0); });
            Program.Test("MySQL new price and history are saved atomically", () =>
            {
                milk.Price = 22.25m; products.Update(milk); var change = products.GetPriceHistory(milk.Id).Single();
                Program.Assert(change.OldPrice == 20.50m && change.NewPrice == 22.25m);
            });
            Program.Test("MySQL stock arrival adds to the existing quantity", () =>
            { products.AddStock(milk.Id, 3); Program.Assert(Reload(milk.Id).Quantity == 13); });
            SaleReceipt receipt = null;
            Program.Test("MySQL sale uses database prices, persists receipt, snapshots and stock", () =>
            {
                var stale = new Product { Id = milk.Id, Name = milk.Name, Price = 1 };
                receipt = sales.CheckoutSale([new CartItem { Product = stale, Quantity = 2 }]);
                var items = sales.GetItems(receipt.Id); Program.Assert(receipt.Total == 44.50m && items.Single().CostPrice == 12 && Reload(milk.Id).Quantity == 11);
                Program.Assert(sales.GetSales(DateTime.Today, DateTime.Today).Any(s => s.Id == receipt.Id));
                var stats = sales.GetStats(); Program.Assert(stats.TodayRevenue == 44.50m && stats.TodayProfit == 20.50m);
                Program.Assert(sales.GetRevenueByDay(7).Count == 7 && sales.GetRevenueByDay(7).Last().Total == 44.50m);
            });
            Program.Test("MySQL insufficient stock leaves all tables unchanged", () =>
            {
                var count = SalesCount(); var stock = Reload(milk.Id).Quantity;
                Program.Throws<InvalidOperationException>(() => sales.CheckoutSale([new CartItem { Product = milk, Quantity = 100 }]));
                Program.Assert(SalesCount() == count && Reload(milk.Id).Quantity == stock);
            });
            Program.Test("MySQL repeated cart lines cannot bypass stock validation", () =>
            {
                var count = SalesCount();
                Program.Throws<InvalidOperationException>(() => sales.CheckoutSale([
                    new CartItem { Product = milk, Quantity = 6 }, new CartItem { Product = milk, Quantity = 6 }]));
                Program.Assert(SalesCount() == count && Reload(milk.Id).Quantity == 11);
            });
            Program.Test("MySQL rejects invalid quantities before opening sale transaction", () =>
            {
                Program.Throws<InvalidOperationException>(() => sales.CheckoutSale([new CartItem { Product = milk, Quantity = -1 }]));
                Program.Throws<InvalidOperationException>(() => products.AddStock(milk.Id, 0));
                Program.Assert(Reload(milk.Id).Quantity == 11);
            });
            Program.Test("MySQL item-write failure rolls back receipt and inventory", () =>
            {
                store.Run(s => s.Execute(Sql.TextOnly("CREATE TRIGGER reject_item BEFORE INSERT ON sale_items FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'forced failure'")));
                var count = SalesCount();
                try { Program.Throws<MySqlException>(() => sales.CheckoutSale([new CartItem { Product = milk, Quantity = 1 }])); }
                finally { store.Run(s => s.Execute(Sql.TextOnly("DROP TRIGGER reject_item"))); }
                Program.Assert(SalesCount() == count && Reload(milk.Id).Quantity == 11);
            });
            Program.Test("MySQL two simultaneous sales cannot oversell the last unit", () =>
            {
                var last = Persist(Add("LAST", 1)); using var gate = new ManualResetEventSlim(false);
                Task<bool> Buy() => Task.Run(() =>
                {
                    gate.Wait();
                    try { sales.CheckoutSale([new CartItem { Product = last, Quantity = 1 }]); return true; }
                    catch (InvalidOperationException) { return false; }
                });
                var a = Buy(); var b = Buy(); gate.Set(); var outcomes = Task.WhenAll(a, b).GetAwaiter().GetResult();
                Program.Assert(outcomes.Count(x => x) == 1 && Reload(last.Id).Quantity == 0);
            });
            Program.Test("MySQL reverse-order baskets use consistent lock order", () =>
            {
                var a = Persist(Add("LOCK-A")); var b = Persist(Add("LOCK-B")); using var gate = new ManualResetEventSlim(false);
                Task<SaleReceipt> Buy(Product x, Product y) => Task.Run(() =>
                { gate.Wait(); return sales.CheckoutSale([new CartItem { Product = x, Quantity = 1 }, new CartItem { Product = y, Quantity = 1 }]); });
                var first = Buy(a, b); var second = Buy(b, a); gate.Set(); Task.WhenAll(first, second).GetAwaiter().GetResult();
                Program.Assert(Reload(a.Id).Quantity == 8 && Reload(b.Id).Quantity == 8);
                Program.Assert(sales.GetItems(second.Result.Id).Select(i => i.ProductId).SequenceEqual(new int?[] { b.Id, a.Id }));
            });
            Program.Test("MySQL deleting product preserves receipt snapshots", () =>
            {
                products.Delete(milk.Id); var item = sales.GetItems(receipt.Id).Single();
                Program.Assert(item.ProductId == null && item.Price == 22.25m && item.ProductName == milk.Name && products.GetPriceHistory(milk.Id).Count == 0);
            });
            Program.Test("MySQL demo recipe preserves 16 products and week of sales; seed is idempotent", () =>
            {
                store.Run(s => { s.Execute(Sql.TextOnly("DELETE FROM sales")); s.Execute(Sql.TextOnly("DELETE FROM products")); });
                var demo = typeof(Database).Assembly.GetType("InventoryApp.Data.DemoData", true).GetMethod("Seed");
                demo.Invoke(null, [store]); var count = SalesCount(); demo.Invoke(null, [store]);
                Program.Assert(products.GetAll().Count == 16 && count > 0 && count == SalesCount() && sales.GetRevenueByDay(7).Count == 7);
            });
        }
        finally
        {
            if (!testDatabase.StartsWith("easydo_test_", StringComparison.Ordinal) || testDatabase.Length != 44)
                throw new InvalidOperationException("Invalid test database cleanup target.");
            server.Run(s => s.Execute(Sql.TextOnly($"DROP DATABASE `{testDatabase}`")));
        }
    }
}
