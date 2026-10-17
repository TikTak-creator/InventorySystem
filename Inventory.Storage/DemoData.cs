namespace Inventory.Storage;

internal static class DemoData
{
    public static void Seed(Store store)
    {
        store.Transaction(session =>
        {
            if (session.Scalar<int>(Sql.TextOnly("SELECT COUNT(*) FROM products")) > 0) return;
            SeedProductsAndSales(session);
        });
    }
    private static void SeedProductsAndSales(Session session)
    {
        var products = new (string Sku, string Name, string Cat, decimal Buy, decimal Sell, int Qty, int Min)[]
        {
            ("PRD-001", "Молоко «Ак-Сүт» 3,2% 1 л",         "Молочные",        62,  79, 48, 10),
            ("PRD-002", "Кефир 2,5% 0,9 л",                 "Молочные",        55,  72,  6, 10),
            ("PRD-003", "Сыр «Российский» 1 кг",            "Молочные",       520, 690, 14,  5),
            ("PRD-004", "Хлеб белый «Нан»",                 "Хлеб",            22,  30, 35, 15),
            ("PRD-005", "Лепёшка тандырная",                "Хлеб",            25,  35,  4, 10),
            ("PRD-006", "Чай «Пиала» чёрный 100 пак.",      "Бакалея",        180, 245, 26,  8),
            ("PRD-007", "Сахар 1 кг",                       "Бакалея",         78,  95, 60, 20),
            ("PRD-008", "Рис «Девзира» 1 кг",               "Бакалея",        160, 210,  3, 10),
            ("PRD-009", "Масло подсолнечное 1 л",           "Бакалея",        135, 170, 22, 10),
            ("PRD-010", "Вода «Легенда» 1,5 л",             "Напитки",         30,  45, 80, 24),
            ("PRD-011", "Coca-Cola 1 л",                    "Напитки",         65,  90,  0, 12),
            ("PRD-012", "Сок «Gracio» 1 л",                 "Напитки",         95, 130, 18,  8),
            ("PRD-013", "Шоколад «Alpen Gold» 90 г",        "Сладости",        70,  99, 40, 15),
            ("PRD-014", "Печенье «Юбилейное»",              "Сладости",        48,  65,  9, 12),
            ("PRD-015", "Мыло «Dove» 100 г",                "Бытовая химия",   85, 120, 15,  6),
            ("PRD-016", "Стиральный порошок «Ariel» 3 кг",  "Бытовая химия",  690, 890,  7,  4),
        };

        var ids = new List<int>();
        foreach (var p in products)
        {
            var cmd = Sql.TextOnly(
                @"INSERT INTO products (sku, name, category, purchase_price, price, quantity, min_quantity)
                  VALUES (@sku, @name, @cat, @buy, @sell, @qty, @min)")
                .Bind("sku", p.Sku).Bind("name", p.Name).Bind("cat", p.Cat)
                .Bind("buy", p.Buy).Bind("sell", p.Sell).Bind("qty", p.Qty).Bind("min", p.Min);
            session.Execute(cmd);
            ids.Add(session.Scalar<int>(Sql.TextOnly("SELECT LAST_INSERT_ID()")));
        }

        // Демо-история продаж за последние 7 дней (для графика на главной)
        var rnd = new Random(42);
        for (int day = 6; day >= 0; day--)
        {
            int salesCount = rnd.Next(4, 9);
            for (int s = 0; s < salesCount; s++)
            {
                var date = DateTime.Today.AddDays(-day).AddHours(rnd.Next(9, 20)).AddMinutes(rnd.Next(0, 60));
                if (date > DateTime.Now) date = DateTime.Now.AddMinutes(-rnd.Next(5, 120));

                var lines = Enumerable.Range(0, rnd.Next(1, 4))
                    .Select(_ => rnd.Next(products.Length)).Distinct()
                    .Select(i => (Index: i, Qty: rnd.Next(1, 4))).ToList();
                decimal total = lines.Sum(l => products[l.Index].Sell * l.Qty);

                var saleCmd = Sql.TextOnly("INSERT INTO sales (created_at, total) VALUES (@d, @t)")
                    .Bind("d", date).Bind("t", total);
                session.Execute(saleCmd);
                var saleId = session.Scalar<int>(Sql.TextOnly("SELECT LAST_INSERT_ID()"));

                foreach (var l in lines)
                {
                    var p = products[l.Index];
                    var itemCmd = Sql.TextOnly(
                        @"INSERT INTO sale_items (sale_id, product_id, product_name, quantity, price, cost_price)
                          VALUES (@s, @p, @n, @q, @pr, @c)")
                        .Bind("s", saleId).Bind("p", ids[l.Index]).Bind("n", p.Name)
                        .Bind("q", l.Qty).Bind("pr", p.Sell).Bind("c", p.Buy);
                    session.Execute(itemCmd);
                }
            }
        }

        // Демо-история изменения цен
        var ph = Sql.TextOnly(
            @"INSERT INTO price_history (product_id, old_price, new_price, changed_at) VALUES
              (@a, 72, 79, NOW() - INTERVAL 5 DAY),
              (@b, 230, 245, NOW() - INTERVAL 3 DAY),
              (@c, 95, 90, NOW() - INTERVAL 1 DAY)")
            .Bind("a", ids[0]).Bind("b", ids[5]).Bind("c", ids[10]);
        session.Execute(ph);
    }
}
