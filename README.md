<div align="center">

<img src="docs/assets/stockflow-jjk-hero.png" width="100%" alt="StockFlow — Jujutsu Kaisen fan-art banner" />

# STOCKFLOW

**Organize your products. Track your stock. Keep your store in view.**

<img alt="C sharp" src="https://img.shields.io/badge/C%23-9F6AFF?style=flat-square" />
<img alt=".NET 8" src="https://img.shields.io/badge/.NET-8-512BD4?style=flat-square" />
<img alt="Windows Forms" src="https://img.shields.io/badge/Windows-Forms-F02549?style=flat-square" />
<img alt="MySQL 8" src="https://img.shields.io/badge/MySQL-8-4479A1?style=flat-square" />


<p><strong>English</strong> &nbsp;|&nbsp; <a href="readme_ru.md">Русский</a></p>
<p><a href="#users">For users</a> &nbsp;·&nbsp; <a href="#developers">For developers</a> &nbsp;·&nbsp; <a href="#roadmap">Roadmap</a> &nbsp;·&nbsp; <a href="#branches">Choose a version</a></p>

</div>

---

> **DOMAIN: INVENTORY · main**<br />
> The original StockFlow desktop application for a small store: products, prices, stock receipts, sales and reports. This branch preserves the implementation before EasyDo and the server/API split.

> [!NOTE]
> This branch connects the Windows application directly to MySQL. Server login, roles, the HTTPS API and the C# SDK are available in [redbox/server-api](https://github.com/TikTak-creator/InventorySystem/tree/redbox/server-api#readme). The cover represents the project as a whole; the features below describe this branch.

<table>
<tr>
<td width="50%" valign="top"><h3>I want to use it</h3><p>Explore the screens, configure a database and start managing products and sales.</p><a href="#users"><strong>Open the user guide →</strong></a></td>
<td width="50%" valign="top"><h3>I want to develop it</h3><p>Build from source, understand the code and choose the right development branch.</p><a href="#developers"><strong>Open the developer guide →</strong></a></td>
</tr>
</table>

<a id="users"></a>

## 01 / Your store at a glance

| Area | Available features |
| :--- | :--- |
| **Dashboard** | Product count, daily revenue and profit, low-stock items, receipt count, average sale, seven-day revenue chart and recent sales |
| **Products** | Search by name/SKU/category, category and low-stock filters, add/edit/delete products, stock receipts and markup calculation |
| **Sales** | Checkout with a cart, stock checks, transactional receipts, history by date and receipt details |
| **Stock** | Quantities, availability status, inventory value, stock receipts and product price history |

Receipts preserve product names, sale prices and costs at checkout. Changing a price or deleting a product does not rewrite sales history. Price changes are recorded in `price_history`.

### Interface

<details open>
<summary><strong>Explore the application screens</strong></summary>

| Store dashboard | Product catalog |
| :---: | :---: |
| ![StockFlow dashboard](screenshots/MainForm.png) | ![StockFlow products](screenshots/ProductsView.png) |
| **Checkout and cart** | **Stock and price history** |
| ![StockFlow checkout](screenshots/SaleForm.png) | ![StockFlow stock](screenshots/StockView.png) |

<sub>The application interface is in Russian. The README is available in English and Russian. Screenshots show the original version.</sub>

</details>

### Start using the application

1. Ask the person deploying the application to configure MySQL as described below.
2. Run `StockFlow.exe` from the published output folder, keeping `appsettings.json` beside it.
3. Use the Dashboard, Products, Sales and Stock sections. This version has no application login or user roles.
4. Add products or use the optional demo data, receive stock and complete a sale through the cart.

| Key | Action |
| :--- | :--- |
| `F5` | Refresh the current section |
| `Enter` / double-click | Edit a product |
| `Delete` | Delete a product or remove a cart item, depending on the window |
| `F2` | Complete a sale in the checkout window |

**Connection error?** Check that MySQL is running, the connection string is correct and the database account has the required permissions. Database initialization runs when the application starts.

**How is profit calculated?** The sum of `quantity × (sale price − cost)` for sold items. It excludes taxes and operating expenses.

<a id="developers"></a>

## 02 / Build and run

**Requirements:** Windows, .NET 8 SDK and MySQL 8. A framework-dependent published application needs .NET Desktop 8 Runtime. The `.slnx` solution requires .NET SDK 9.0.200+; with SDK 8, use the individual project commands below.

**1. Configure a local database**

Edit [InventoryApp/appsettings.json](InventoryApp/appsettings.json) with your local connection details:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Port=3306;Database=inventory_db;User ID=stockflow;Password=<your-local-password>;CharSet=utf8mb4;"
  },
  "SeedDemoData": false
}
```

The placeholder password must be replaced locally. Database creation and table initialization are automatic; the account needs the corresponding permissions. Set `SeedDemoData` to `true` to populate an empty catalog with 16 example products and a week of sales. SQL for manual deployment is in [database/schema.sql](database/schema.sql).

This branch stores database credentials in a local configuration file and accesses MySQL from the desktop application. Keep real credentials out of commits. For centrally managed access, use the server/API branch.

**2. Build and start from the repository root**

```powershell
dotnet restore InventoryApp/InventoryApp.csproj
dotnet build InventoryApp/InventoryApp.csproj -c Release
dotnet run --project InventoryApp
```

**3. Publish a Windows build**

```powershell
dotnet publish InventoryApp -c Release -r win-x64 --self-contained false -o publish/client
```

### Stack and architecture

| Component | Built with | Responsibility |
| :--- | :--- | :--- |
| **Desktop application** | C# · .NET 8 · WinForms | Russian-language Windows interface, navigation and forms |
| **Data access** | MySqlConnector · ADO.NET | Parameterized SQL, product repositories, receipts and statistics |
| **Database** | MySQL 8 · InnoDB | Products, sales, receipt items and price history |


```mermaid
flowchart LR
    UI["Windows interface"] --> DATA["ADO.NET repositories"]
    DATA -->|MySqlConnector| DB[(MySQL)]
    classDef app fill:#181321,stroke:#9F6AFF,color:#ffffff;
    classDef data fill:#241018,stroke:#F02549,color:#ffffff;
    class UI app;
    class DATA,DB data;
```

Sales run in one transaction with `SELECT ... FOR UPDATE` stock locks. A failed sale rolls back its receipt and stock changes.

| Source | Responsibility |
| :--- | :--- |
| [Program.cs](InventoryApp/Program.cs) | Application startup and database initialization |
| [Models](InventoryApp/Models) | Products, sales, cart items and price changes |
| [Database.cs](InventoryApp/Data/Database.cs) | MySQL adapter and initialization |
| [ProductRepository.cs](InventoryApp/Data/ProductRepository.cs) | Catalog, stock receipts and price history |
| [SaleRepository.cs](InventoryApp/Data/SaleRepository.cs) | Transactional checkout and statistics |
| [UI](InventoryApp/UI) | Main window, views, dialogs and appearance |

```text
products ──< sale_items >── sales
    │
    └──< price_history
```

### Verification

No automated test suite is included on this branch. After changing the application, verify startup, catalog editing, stock receipts, checkout, price history and reporting against a development database.

<a id="roadmap"></a>

## 03 / Available now and next steps

- [x] Product catalog, search, prices, stock receipts and inventory levels.
- [x] Transactional sales, receipt history and store metrics.
- [x] Dark Windows interface, charts and keyboard shortcuts.

- [ ] Asynchronous screen loading and cancellation.
- [ ] Returns, write-offs and a stock movement ledger.
- [ ] Suppliers, purchasing, multiple warehouses and import/export.
- [ ] Full financial accounting and tax/fiscal workflows.

EasyDo is implemented in `redbox/easydo-baseline`; the server, authentication and API are implemented in `redbox/server-api`. Synchronous database work can block the interface. This version serves one store and does not include full accounting. Direct quantity editing retains last-save behavior during concurrent edits.

<a id="branches"></a>

## 04 / Choose a version

| Branch | Contents |
| :--- | :--- |
| [main](https://github.com/TikTak-creator/InventorySystem/tree/main) | Original desktop application with direct MySQL access |
| [redbox/easydo-baseline](https://github.com/TikTak-creator/InventorySystem/tree/redbox/easydo-baseline) | EasyDo migration with the original interface and direct MySQL access |
| [redbox/server-api](https://github.com/TikTak-creator/InventorySystem/tree/redbox/server-api) | Separate server and client, HTTPS API, login, roles and C# SDK |

The German portfolio and detailed API/security guides belong to the server version: [Portfolio · DE](https://github.com/TikTak-creator/InventorySystem/blob/redbox/server-api/docs/PORTFOLIO.md) · [API](https://github.com/TikTak-creator/InventorySystem/blob/redbox/server-api/docs/API.md) · [Security](https://github.com/TikTak-creator/InventorySystem/blob/redbox/server-api/docs/SECURITY.md).

For contributions, start from the appropriate branch, describe the intended behavior and include relevant validation. Keep credentials and production data out of commits. The English and Russian root READMEs should describe the same branch behavior.

<details>
<summary><strong>About the artwork and languages</strong></summary>

The banner is the project owner's edited version of unofficial **Jujutsu Kaisen** fan art featuring Yuji Itadori, Aoi Todo, Suguru Geto and Satoru Gojo. [Artwork history](docs/assets/ART-DIRECTION.md).

[README.md](README.md) is the English edition; [readme_ru.md](readme_ru.md) is the Russian edition. Supporting technical guides are currently in Russian, and the portfolio is in German. Switching README language does not change the application interface.

</details>

---

<div align="center">
<sub><strong>STOCKFLOW</strong> · Inventory under control · Built with C# &amp; WinForms</sub><br />
<sub><a href="#users">Get started</a> · <a href="#developers">Explore the code</a> · <a href="readme_ru.md">Русский</a></sub>
</div>
